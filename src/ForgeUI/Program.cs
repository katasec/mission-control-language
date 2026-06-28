using ForgeMission.Cli;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using ForgeUI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Load the mission once at startup.
var missionPath = builder.Configuration["MissionPath"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "mission.mcl");

var missionDir = Path.GetDirectoryName(Path.GetFullPath(missionPath))!;
var source     = await File.ReadAllTextAsync(missionPath);
var ast        = MclParser.Parse(source);
var lockPath   = Path.Combine(missionDir, "mcl.lock");
var lockFile   = LockFileIO.Read(lockPath);
var expertDefs = ExpertResolver.ResolveAll(lockFile, missionDir, verbose: null, warnings: Console.Error);

// Build LLM runner from forge.toml if present; fall back to exec stub.
IExpertRunner defaultRunner;
var manifest = ForgeTomlReader.TryRead(missionPath);
if (manifest?.Providers is { Count: > 0 } providers &&
    providers.TryGetValue("default", out var profile))
{
    defaultRunner = ProviderClientBuilder.Build(profile);
}
else
{
    defaultRunner = new ExecExpertRunner();
    Console.Error.WriteLine("ForgeUI: no forge.toml provider found — LLM experts will not work.");
}

builder.Services.AddSingleton(ast);
builder.Services.AddSingleton(expertDefs);
builder.Services.AddSingleton<IExpertRunner>(defaultRunner);
builder.Services.AddScoped<MissionService>();
builder.Services.AddScoped<SessionStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
