using System.Net.Http.Json;
using System.Security.Claims;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeMission.Runner.Contracts;
using ForgeUI;
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

    // Platform-key issuance (42.5 ①): a second scheme validating the CLI's Entra access token.
    // Only when OIDC is configured — local dev has no real token issuer.
    auth.AddPlatformKeyBearer(builder.Configuration);
}

builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

// Identity + membership services (38.4).
builder.Services.AddScoped<MemberProvisioningService>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<InviteService>();

// Metering & billing (39.2): per-user cost-meter + balance ledger. Singleton — stateless over the
// ledger store; grants on provisioning, checks/debits on each agent run.
builder.Services.AddSingleton<BillingService>();

// Mission execution moved to the containerised runner (Phase 39.1). The orchestrator no longer
// loads missions or holds provider keys — it calls the runner over HTTP. RunnerBaseUrl points at
// the warm runner (ACA); default localhost for `dotnet run` alongside the runner.
var runnerBaseUrl = builder.Configuration["RunnerBaseUrl"] ?? "http://localhost:5266";
builder.Services.AddHttpClient<MissionRunnerClient>(c =>
{
    c.BaseAddress = new Uri(runnerBaseUrl);
    c.Timeout     = TimeSpan.FromMinutes(4); // > the runner's 3-min per-run ceiling
});

// Probe the runner for the missions it can actually execute (provider key present) so we bind only
// the handles whose mission is loadable — the per-mission key behaviour (38.5 task 7) now keyed off
// the runner. Resilient to boot ordering: retry briefly, then fall back to no agents rather than
// crash the host (Rooms still work without agents).
var availableMissionRefs = await ProbeRunnerMissionsAsync(runnerBaseUrl);
Console.Error.WriteLine(availableMissionRefs.Count == 0
    ? $"ForgeUI: runner at {runnerBaseUrl} advertised no missions — agents disabled; /rooms still available."
    : $"ForgeUI: runner advertises {availableMissionRefs.Count} mission(s): {string.Join(", ", availableMissionRefs)}.");

// Rooms agent bridge (38.2/38.5): the @handle directory (GAL) resolves a mention to a mission ref,
// then the invoker assembles room-scoped context and runs it in the runner. All singleton-safe.
builder.Services.AddSingleton(new AgentRegistry(availableMissionRefs));
builder.Services.AddSingleton<RoomContextAssembler>();
builder.Services.AddSingleton<RoomAgentInvoker>();
// Add/remove an agent from a room (38.5 task 3) — provisioner-gated membership management.
builder.Services.AddSingleton<RoomAgentMembershipService>();
// Provisioner-only room admin (rename) — kept out of the agent-membership service.
builder.Services.AddSingleton<RoomAdminService>();

// Room delivery (38.4): in-proc fan-out to the Blazor client + external SignalR clients,
// and the one membership-checked send path shared by both.
builder.Services.AddSingleton<RoomBroadcaster>();
builder.Services.AddSingleton<RoomMessageService>();

// Onboarding: give a brand-new user a private "room of two" with @assistant so they
// land in a usable chat instead of an empty rooms list.
builder.Services.AddScoped<StarterRoomService>();

// User-initiated "+ New room" — name it and pick which agents join up front.
builder.Services.AddScoped<RoomCreationService>();

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

app.MapPlatformKeys(); // 42.5 ①: POST /platform/keys (Entra-bearer issuance)
app.MapBlazorHub();
app.MapHub<ChatHub>("/hubs/chat");
app.MapFallbackToPage("/_Host");

app.Run();
return;

static string Local(string? returnUrl)
    => !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/rooms";

// Ask the runner which missions it can execute (39.1). Retries briefly to tolerate boot ordering,
// then falls back to an empty set — a runner outage disables agents but never crashes the host.
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
            Console.Error.WriteLine($"ForgeUI: runner probe attempt {attempt}/5 failed: {ex.Message}");
        }
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
    return new HashSet<string>(StringComparer.Ordinal);
}
