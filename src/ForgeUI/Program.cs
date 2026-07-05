using ForgeMission.Core.Manifest;
using ForgeMission.Rooms.Data;
using ForgeUI.Hubs;
using ForgeUI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

// Rooms persistence — two connection slots (same DB initially; replica later is config).
var readConnection = builder.Configuration.GetConnectionString("ReadConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:ReadConnection is not configured.");
var writeConnection = builder.Configuration.GetConnectionString("WriteConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:WriteConnection is not configured.");
builder.Services.AddRoomsData(readConnection, writeConnection);
builder.Services.AddScoped<StubIdentity>();

// Resolve API key from env (set MCL_API_KEY in your shell profile).
var missionDir = builder.Configuration["MissionDir"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "missions");
missionDir = Path.GetFullPath(missionDir);

// Read key from the first forge.toml that has one, or fall back to empty.
var apiKey = ForgeTomlReader.TryRead(Path.Combine(missionDir, "hallucination-guard", "mission.mcl"))
                 ?.Providers?.GetValueOrDefault("default")?.ApiKey
             ?? string.Empty;

var keyPrefix = apiKey is { Length: > 10 } ? apiKey[..10] + "..." : "(empty)";
Console.Error.WriteLine($"ForgeUI: API key length = {apiKey.Length}, prefix = {keyPrefix}");

// Rooms (38.1) has no LLM in the path, so a missing key disables mission chat
// only — it no longer kills the host.
MissionRegistry registry;
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("ForgeUI: API key is empty — mission chat disabled (set MCL_API_KEY); /rooms still available.");
    registry = new MissionRegistry([]);
}
else
{
    registry = await MissionRegistry.LoadAsync(
    [
        ("ChatGPT",  "Raw LLM — no verification",                    Path.Combine(missionDir, "vanilla",             "mission.mcl")),
        ("Forge",    "LLM + deterministic verifier, retries on fail", Path.Combine(missionDir, "hallucination-guard", "mission.mcl")),
    ],
    apiKey);
}

builder.Services.AddSingleton(registry);
builder.Services.AddScoped<MissionService>();
builder.Services.AddScoped<SessionStore>();

// Rooms agent bridge (38.2): resolve @handle → mission, assemble room-scoped context,
// invoke the mission off the hub call, and stream the result back. All singleton-safe.
builder.Services.AddSingleton<AgentCatalog>();
builder.Services.AddSingleton<RoomContextAssembler>();
builder.Services.AddSingleton<RoomAgentInvoker>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Self-healing schema + seed data in Development ONLY — never auto-migrate in prod.
if (app.Environment.IsDevelopment())
{
    var factory = app.Services.GetRequiredService<IDbContextFactory<RoomsDbContext>>();
    await using (var db = await factory.CreateDbContextAsync())
        await db.Database.MigrateAsync();
    await RoomsSeeder.SeedAsync(factory);
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapHub<ChatHub>("/hubs/chat");
app.MapFallbackToPage("/_Host");

app.Run();
