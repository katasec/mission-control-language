using ForgeMission.Api;

// ForgeAPI — the tier-1 API-gateway edge for the hosted /v1 endpoint (Phase 42.6). Terminates the
// public wire on forge.katasec.com, and (as tasks land) authenticates the platform key, routes a
// handle → mission, meters, and reverse-proxies to the internal runner. The runner never faces the
// internet and holds no DB creds; this gateway is its only ingress.
//
// Foundation slice (task 3): health + a streaming /v1 pass-through to the runner. Auth (task 4),
// per-handle routing (task 5), and billing (task 6) wrap the relay next.
var builder = WebApplication.CreateSlimBuilder(args);

// The internal runner's base address — same convention ForgeUI uses. In cloud this is the runner's
// private ACA ingress; locally, the `dotnet run` runner port.
var runnerBaseUrl = builder.Configuration["RunnerBaseUrl"] ?? "http://localhost:5266";
builder.Services.AddHttpClient("runner", c =>
{
    c.BaseAddress = new Uri(runnerBaseUrl);
    c.Timeout = Timeout.InfiniteTimeSpan; // a run streams for tens of seconds; the client sets the bound.
});

var app = builder.Build();

app.MapGet("/health", (RequestDelegate)(ctx =>
{
    ctx.Response.ContentType = "application/json";
    return ctx.Response.WriteAsync("""{"status":"ok"}""", ctx.RequestAborted);
}));

// Pass-through the whole /v1 wire (Anthropic /v1/messages + OpenAI /v1/chat|responses|models) to the
// runner's internal door. Task 5 puts a /m/{handle} prefix in front to select the mission.
app.Map("/v1/{**rest}", async (HttpContext ctx, IHttpClientFactory clients) =>
{
    var runner = clients.CreateClient("runner");
    await WireProxy.ForwardAsync(ctx, runner, ctx.Request.Path + ctx.Request.QueryString);
});

app.Run();
