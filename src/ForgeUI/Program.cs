using System.Security.Claims;
using ForgeMission.Core.Manifest;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeUI.Hubs;
using ForgeUI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

// Behind a TLS-terminating reverse proxy (Azure Container Apps), honor X-Forwarded-Proto so the
// app knows requests are HTTPS — otherwise OIDC builds http:// redirect URIs (rejected by Entra)
// and the Secure correlation cookie is dropped. KnownNetworks/Proxies cleared because the proxy
// hop is inside the managed environment (not a fixed IP we can allow-list).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rooms persistence — two connection slots (same DB initially; replica later is config).
var readConnection = builder.Configuration.GetConnectionString("ReadConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:ReadConnection is not configured.");
var writeConnection = builder.Configuration.GetConnectionString("WriteConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:WriteConnection is not configured.");
builder.Services.AddRoomsData(readConnection, writeConnection);

// --- Identity (38.4) — federated OIDC via Entra External ID, cookie session ---------------
// Entra External ID is wired as a *standard* OIDC provider (no B2C custom policies), so the
// exit from any one IdP stays cheap. Google/Apple are federated *inside* the Entra tenant, so
// the app has a single OIDC registration. When OIDC is unconfigured (local dev), a dev sign-in
// endpoint drives the same cookie + provisioning path — only the identity source differs.
var oidcAuthority = builder.Configuration["Oidc:Authority"];
var oidcClientId = builder.Configuration["Oidc:ClientId"];
var oidcConfigured = !string.IsNullOrWhiteSpace(oidcAuthority) && !string.IsNullOrWhiteSpace(oidcClientId);

var auth = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = oidcConfigured
        ? OpenIdConnectDefaults.AuthenticationScheme
        : CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "forge.auth";
    options.LoginPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

if (oidcConfigured)
{
    auth.AddOpenIdConnect(options =>
    {
        options.Authority = oidcAuthority;
        options.ClientId = oidcClientId;
        options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
        options.CallbackPath = builder.Configuration["Oidc:CallbackPath"] ?? "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = true; // sub -> NameIdentifier
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = ctx =>
            {
                // Stamp a stable, scheme-level issuer for provisioning — decoupled from token
                // `iss` quirks (one broker = one issuer namespace; `sub` is unique within it).
                if (ctx.Principal?.Identity is ClaimsIdentity identity &&
                    identity.FindFirst(ForgeClaims.Issuer) is null)
                {
                    identity.AddClaim(new Claim(ForgeClaims.Issuer, "entra-external-id"));
                }
                return Task.CompletedTask;
            },
        };
    });
}

builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

// Identity + membership services (38.4).
builder.Services.AddScoped<MemberProvisioningService>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<InviteService>();

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
        ("ChatGPT",   "Raw LLM — no verification",                     Path.Combine(missionDir, "vanilla",             "mission.mcl")),
        ("Forge",     "LLM + deterministic verifier, retries on fail", Path.Combine(missionDir, "hallucination-guard", "mission.mcl")),
        ("Assistant", "General assistant, answers LLM-verified",       Path.Combine(missionDir, "assistant",           "mission.mcl")),
    ],
    apiKey);
}

builder.Services.AddSingleton(registry);
builder.Services.AddScoped<MissionService>();
builder.Services.AddScoped<SessionStore>();

// Rooms agent bridge (38.2/38.5): the @handle directory (GAL) resolves a mention to a mission,
// then the invoker assembles room-scoped context, runs it off the hub call, and streams the
// result back. All singleton-safe.
builder.Services.AddSingleton<AgentRegistry>();
builder.Services.AddSingleton<RoomContextAssembler>();
builder.Services.AddSingleton<RoomAgentInvoker>();

// Room delivery (38.4): in-proc fan-out to the Blazor client + external SignalR clients,
// and the one membership-checked send path shared by both.
builder.Services.AddSingleton<RoomBroadcaster>();
builder.Services.AddSingleton<RoomMessageService>();

// Onboarding: give a brand-new user a private "room of two" with @assistant so they
// land in a usable chat instead of an empty rooms list.
builder.Services.AddScoped<StarterRoomService>();

var app = builder.Build();

// Must run before auth/redirect logic so the forwarded scheme is applied first.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Self-healing schema + dev test data in Development ONLY — never auto-migrate in prod.
{
    var factory = app.Services.GetRequiredService<IDbContextFactory<RoomsDbContext>>();
    if (app.Environment.IsDevelopment())
    {
        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.MigrateAsync();
        await RoomsSeeder.SeedAsync(factory);
    }

    // Essential product data (built-in agent members) in ALL environments — idempotent.
    // Starter rooms reference the @assistant member, so it must exist in prod too.
    // Prod schema is created by the migration job before this runs.
    await RoomsSeeder.SeedEssentialAgentsAsync(factory);
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// --- Auth endpoints (38.4) ----------------------------------------------------------------
// Sign-in/out must happen at HTTP endpoints (not inside the Blazor circuit).
app.MapGet("/auth/login", (string? returnUrl, HttpContext http) =>
{
    var scheme = oidcConfigured
        ? OpenIdConnectDefaults.AuthenticationScheme
        : CookieAuthenticationDefaults.AuthenticationScheme;
    return Results.Challenge(new AuthenticationProperties { RedirectUri = Local(returnUrl) }, [scheme]);
});

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (oidcConfigured)
        await http.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
});

// Dev sign-in (Development only): drives the SAME cookie + provisioning path as real OIDC —
// only the identity source differs. Seeded Alice/Bob map by (issuer=dev, subject); any other
// name provisions a fresh member (useful for the non-member + invite tests).
if (app.Environment.IsDevelopment())
{
    app.MapGet("/auth/dev", async (string user, string? returnUrl, HttpContext http) =>
    {
        var sub = user.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(sub))
            return Results.BadRequest("user required");
        var name = char.ToUpperInvariant(sub[0]) + sub[1..];
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim(ForgeClaims.Issuer, RoomsSeeder.DevIssuer),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Email, $"{sub}@dev.local"),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.LocalRedirect(Local(returnUrl));
    });
}

// Invite accept (38.4): tap link → (sign in if needed) → auto-join with the granted role.
app.MapGet("/invite/{token}", async (string token, HttpContext http,
    MemberProvisioningService provisioning, InviteService invites) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
    {
        var scheme = oidcConfigured
            ? OpenIdConnectDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
        return Results.Challenge(new AuthenticationProperties { RedirectUri = $"/invite/{token}" }, [scheme]);
    }

    var member = await provisioning.ResolveAsync(http.User);
    if (member is null)
        return Results.Unauthorized();

    var result = await invites.AcceptAsync(token, member);
    return result.Status switch
    {
        InviteStatus.Joined => Results.LocalRedirect($"/rooms/{result.RoomId}"),
        InviteStatus.Expired => Results.Content("This invite link has expired.", "text/plain"),
        _ => Results.NotFound("Invite not found."),
    };
});

app.MapBlazorHub();
app.MapHub<ChatHub>("/hubs/chat");
app.MapFallbackToPage("/_Host");

app.Run();
return;

static string Local(string? returnUrl)
    => !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/rooms";
