using ForgeMission.Api;
using ForgeMission.Billing;

// ForgeAPI — the tier-1 API-gateway edge for the hosted /v1 endpoint (Phase 42.6). Terminates the
// public wire on forge.katasec.com, authenticates the platform key, and (as tasks land) routes a
// handle → mission, meters, and reverse-proxies to the internal runner. The runner never faces the
// internet and holds no DB creds; this gateway is its only ingress.
//
// Slice so far: health + a streaming /v1 pass-through (task 3) behind platform-key auth (task 4).
// Per-handle routing (task 5) and billing (task 6) wrap the relay next.
var builder = WebApplication.CreateSlimBuilder(args);

// Source-gen JSON for the gateway's own responses (AOT-clean — relayed /v1 bodies pass through as
// opaque byte streams and are never deserialized here).
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));

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

var app = builder.Build();

app.MapGet("/health", (RequestDelegate)(ctx =>
{
    ctx.Response.ContentType = "application/json";
    return ctx.Response.WriteAsync("""{"status":"ok"}""", ctx.RequestAborted);
}));

// Pass-through the whole /v1 wire (Anthropic /v1/messages + OpenAI /v1/chat|responses|models) to the
// runner's internal door, behind platform-key auth (task 4). Task 5 puts a /m/{handle} prefix in
// front to select the mission.
app.Map("/v1/{**rest}", async (HttpContext ctx, IHttpClientFactory clients) =>
{
    var runner = clients.CreateClient("runner");
    await WireProxy.ForwardAsync(ctx, runner, ctx.Request.Path + ctx.Request.QueryString);
})
.AddEndpointFilter<PlatformKeyAuthFilter>();

app.Run();
