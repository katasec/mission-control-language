using System.Net.Http.Json;
using ForgeMission.Api;
using ForgeMission.Billing;
using ForgeMission.Runner.Contracts;

// ForgeAPI — the tier-1 API-gateway edge for the hosted /v1 endpoint (Phase 42.6). Terminates the
// public wire on forge.katasec.com, authenticates the platform key, and routes a handle → mission
// (task 5a, API A — message-based ExecuteMission/SearchMissions/GetMission/GetAccount/GetRun),
// meters, and reverse-proxies the spec-bound chat wire (API B, 5b) to the internal runner. The
// runner never faces the internet and holds no DB creds; this gateway is its only ingress.
var builder = WebApplication.CreateSlimBuilder(args);

// Source-gen JSON for the gateway's own responses (AOT-clean). Relayed /v1 bodies (API B) still pass
// through as opaque byte streams and are never deserialized here; API A's messages (task 5a) are the
// only bodies this host actually (de)serializes.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default);
    o.SerializerOptions.TypeInfoResolverChain.Insert(1, MessagesJsonContext.Default);
});

// The internal runner's base address — same convention ForgeUI uses. In cloud this is the runner's
// private ACA ingress; locally, the `dotnet run` runner port.
var runnerBaseUrl = builder.Configuration["RunnerBaseUrl"] ?? "http://localhost:5266";
builder.Services.AddHttpClient("runner", c =>
{
    c.BaseAddress = new Uri(runnerBaseUrl);
    c.Timeout = Timeout.InfiniteTimeSpan; // a run streams for tens of seconds; the client sets the bound.
});

// Auth/billing bounded context over authbilling_db (raw Npgsql, AOT-clean). ForgeAPI reads keys +
// balances only — the resolver (member + balance behind a short cache) backs the /v1 auth filter.
// Same shared lib + DB that ForgeUI's room path bills against; keyed by member_id.
var authBillingConnection = builder.Configuration.GetConnectionString("AuthBillingConnection")
    ?? "Host=localhost;Port=5432;Database=authbilling_db;Username=forge_app;Password=forge_app_dev";
builder.Services.AddAuthBilling(authBillingConnection);

// The HMAC key must match ForgeUI's issuer (PlatformKeys:HmacKey), or every verify fails; the dev
// default mirrors ForgeUI's so a locally minted key resolves against a locally run gateway.
builder.Services.AddPlatformKeyResolver(new PlatformKeyResolverOptions
{
    HmacKey = builder.Configuration["PlatformKeys:HmacKey"] ?? "dev-platform-key-hmac-do-not-use-in-prod",
});

// The built-in catalog (task 5a) — a hardcoded entry list filtered against the runner's live
// GET /missions at boot, same precedent AgentRegistry (ForgeUI) already sets: a mission whose
// backing ref the runner doesn't currently advertise (e.g. a missing provider key) simply doesn't
// resolve rather than 500ing.
var availableMissionRefs = await ProbeRunnerMissionsAsync(runnerBaseUrl);
Console.Error.WriteLine(availableMissionRefs.Count == 0
    ? $"ForgeAPI: runner at {runnerBaseUrl} advertised no missions — the catalog will resolve nothing."
    : $"ForgeAPI: runner advertises {availableMissionRefs.Count} mission(s): {string.Join(", ", availableMissionRefs)}.");
builder.Services.AddSingleton<IMissionCatalog>(new StaticMissionCatalog(availableMissionRefs));

// GetRun storage (M6) — in-memory today; blob storage is the target shape (see IRunStore's doc).
builder.Services.AddSingleton<IRunStore, InMemoryRunStore>();
builder.Services.AddSingleton<IArtifactStore, FileArtifactStore>();

builder.Services.AddSingleton<MissionExecutionService>();

var app = builder.Build();

// Idempotent schema bootstrap (CREATE TABLE IF NOT EXISTS, incl. task 5a's client_token column) —
// ForgeAPI owns authbilling_db reads/writes directly and must not depend on ForgeUI having booted
// first to create the tables. Cheap and safe on every boot, same call ForgeUI's Program.cs makes.
await AuthBillingSchema.EnsureCreatedAsync(app.Services.GetRequiredService<Npgsql.NpgsqlDataSource>());

app.MapGet("/health", (RequestDelegate)(ctx =>
{
    ctx.Response.ContentType = "application/json";
    return ctx.Response.WriteAsync("""{"status":"ok"}""", ctx.RequestAborted);
}));

// API A — mission invocation (task 5a): ExecuteMission/SearchMissions/GetMission/GetAccount/GetRun.
app.MapMissionEndpoints();

// API B — pass-through the whole /v1 wire (Anthropic /v1/messages + OpenAI /v1/chat|responses|models)
// to the runner's internal door, behind platform-key auth (task 4). 5b puts a /m/{handle} prefix in
// front to select the mission.
app.Map("/v1/{**rest}", async (HttpContext ctx, IHttpClientFactory clients) =>
{
    var runner = clients.CreateClient("runner");
    await WireProxy.ForwardAsync(ctx, runner, ctx.Request.Path + ctx.Request.QueryString);
})
.AddEndpointFilter<PlatformKeyAuthFilter>();

app.Run();

// Same retry-probe shape as ForgeUI's Program.cs (ProbeRunnerMissionsAsync) — the runner may still be
// starting when this gateway boots, so a few attempts with a short backoff ride out that race.
static async Task<IReadOnlySet<string>> ProbeRunnerMissionsAsync(string baseUrl)
{
    using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
    for (var attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            var missions = await http.GetFromJsonAsync(
                "/missions", RunContractsContext.Default.IReadOnlyListMissionInfo);
            if (missions is not null)
                return missions.Select(m => m.MissionRef).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ForgeAPI: runner probe attempt {attempt}/5 failed: {ex.Message}");
        }
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
    return new HashSet<string>();
}
