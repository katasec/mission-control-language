using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeMission.Core.Resolution;

namespace ForgeMission.Cli;

// forge login (42.5): platform sign-in via loopback auth-code + PKCE against Entra External ID.
// Hand-rolled public client (no MSAL — AOT-first): one localhost listener + one token call.
// Produces Entra tokens proving identity; task 4 exchanges the access token for a platform key.
public static class PlatformLogin
{
    // Public identifiers, not secrets (both appear in the sign-in URL). Env overrides allow
    // pointing at another tenant/environment.
    private static string Authority =>
        Environment.GetEnvironmentVariable("FORGE_AUTH_AUTHORITY")
        ?? "https://forgeids.ciamlogin.com/79c07ac3-4f45-4ea8-9701-94fd2ef1decd";

    private static string ClientId =>
        Environment.GetEnvironmentVariable("FORGE_AUTH_CLIENT_ID")
        ?? "33595d97-0296-4868-9217-dfab35faa314"; // Forge CLI (dev) public client — forge-infra/dev/200-entra/create-cli-app-registration.sh

    // Request an access token for the Rooms app's cli.login scope (the issuance endpoint
    // validates this bearer), alongside the reserved OIDC scopes for id_token + refresh.
    private static string Scope =>
        Environment.GetEnvironmentVariable("FORGE_AUTH_SCOPE")
        ?? "api://4f8a95d6-2d41-416c-a1b9-9177ddec1227/cli.login openid profile email offline_access";

    // Base URL of the platform that issues the key (the forge-ui control plane). Override for local
    // dev against a locally-run ForgeUI.
    private static string PlatformEndpoint =>
        Environment.GetEnvironmentVariable("FORGE_PLATFORM_ENDPOINT")?.TrimEnd('/')
        ?? "https://forge.katasec.com";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<int> RunAsync()
    {
        var (verifier, challenge) = CreatePkcePair();
        var state = RandomUrlSafe(16);

        using var listener = StartLoopbackListener(out var redirectUri);
        var authorizeUrl = BuildAuthorizeUrl(challenge, state, redirectUri);

        Console.WriteLine("Opening your browser to sign in…");
        Console.WriteLine($"(if it doesn't open, visit: {authorizeUrl})");
        OpenBrowser(authorizeUrl);

        var code = await WaitForCallbackAsync(listener, state);
        if (code is null) return 1;

        var tokens = await ExchangeCodeAsync(code, verifier, redirectUri);
        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken)) return 1;

        // Exchange the Entra access token for a platform key (issuance endpoint on the control plane).
        var issued = await IssuePlatformKeyAsync(tokens.AccessToken);
        if (issued is null) return 1;

        var user = issued.Email ?? ReadUserLabel(tokens.IdToken);
        CredentialStore.SavePlatform(new PlatformCredential
        {
            Key = issued.Key,
            User = user,
            Endpoint = PlatformEndpoint,
        });

        Console.WriteLine($"✓ signed in as {user}");
        Console.WriteLine($"  {FormatBalance(issued.BalanceMicroUsd)} credit · key stored in ~/.forge");
        return 0;
    }

    // --- forge whoami / forge logout (T5) -------------------------------------------------------

    // Show the signed-in user + live balance (reads the stored key, calls /me on its endpoint).
    public static async Task<int> WhoAmIAsync()
    {
        var platform = CredentialStore.GetPlatform();
        if (platform is null || string.IsNullOrEmpty(platform.Key))
        {
            Console.Error.WriteLine("Not signed in. Run `forge login`.");
            return 1;
        }

        var me = await FetchMeAsync(platform.Endpoint, platform.Key);
        if (me is null) return 1;

        Console.WriteLine($"Signed in as {me.Email ?? platform.User}");
        Console.WriteLine($"  {FormatBalance(me.BalanceMicroUsd)} credit · {platform.Endpoint}");
        return 0;
    }

    // Clear the stored platform key.
    public static int Logout()
    {
        if (CredentialStore.GetPlatform() is null)
        {
            Console.WriteLine("Not signed in.");
            return 0;
        }

        CredentialStore.ClearPlatform();
        Console.WriteLine("Signed out — platform key removed from ~/.forge.");
        return 0;
    }

    private static async Task<MeResponse?> FetchMeAsync(string endpoint, string key)
    {
        var baseUrl = endpoint.TrimEnd('/');
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var resp = await Http.SendAsync(req);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.Error.WriteLine("Your platform key is no longer valid (expired or revoked). Run `forge login`.");
            return null;
        }

        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"whoami failed ({(int)resp.StatusCode}) at {baseUrl}/me: {json}");
            return null;
        }

        try { return JsonSerializer.Deserialize(json, PlatformLoginJsonContext.Default.MeResponse); }
        catch (JsonException) { Console.Error.WriteLine("whoami returned an unreadable response."); return null; }
    }

    // Exchange the validated Entra access token for a long-lived platform key (fg_live_…).
    private static async Task<IssueKeyResponse?> IssuePlatformKeyAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{PlatformEndpoint}/platform/keys");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var resp = await Http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Key issuance failed ({(int)resp.StatusCode}) at {PlatformEndpoint}/platform/keys: {json}");
            var wwwAuth = resp.Headers.WwwAuthenticate.ToString();
            if (!string.IsNullOrEmpty(wwwAuth)) Console.Error.WriteLine($"  WWW-Authenticate: {wwwAuth}");
            return null;
        }

        try { return JsonSerializer.Deserialize(json, PlatformLoginJsonContext.Default.IssueKeyResponse); }
        catch (JsonException)
        {
            Console.Error.WriteLine("Key issuance returned an unreadable response.");
            return null;
        }
    }

    private static string FormatBalance(long microUsd) => $"${microUsd / 1_000_000.0:0.00}";

    // --- PKCE -----------------------------------------------------------------------------

    private static (string Verifier, string Challenge) CreatePkcePair()
    {
        var verifier = RandomUrlSafe(32);
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string RandomUrlSafe(int bytes) =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(bytes));

    // --- Loopback listener ----------------------------------------------------------------

    private static HttpListener StartLoopbackListener(out string redirectUri)
    {
        var port = FindFreePort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        redirectUri = $"http://localhost:{port}";
        return listener;
    }

    private static int FindFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static async Task<string?> WaitForCallbackAsync(HttpListener listener, string expectedState)
    {
        var timeout = Task.Delay(TimeSpan.FromMinutes(5));
        while (true)
        {
            var getContext = listener.GetContextAsync();
            if (await Task.WhenAny(getContext, timeout) == timeout)
            {
                Console.Error.WriteLine("Timed out waiting for the browser sign-in (5 minutes).");
                return null;
            }

            var ctx = await getContext;
            var query = ctx.Request.QueryString;
            var code = query["code"];
            var error = query["error"];

            if (code is null && error is null) { Respond(ctx, 404, "Not the sign-in callback."); continue; } // favicon etc.

            if (error is not null)
            {
                Respond(ctx, 200, "Sign-in was cancelled — you can close this tab.");
                Console.Error.WriteLine($"Sign-in error: {error}: {query["error_description"]}");
                return null;
            }

            if (query["state"] != expectedState)
            {
                Respond(ctx, 400, "State mismatch.");
                Console.Error.WriteLine("Sign-in error: state mismatch — try again.");
                return null;
            }

            Respond(ctx, 200, "✓ Signed in — return to your terminal.");
            return code;
        }
    }

    private static void Respond(HttpListenerContext ctx, int status, string message)
    {
        var body = Encoding.UTF8.GetBytes($"<html><body style=\"font-family:system-ui;margin:3em\"><h2>{message}</h2></body></html>");
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.OutputStream.Write(body);
        ctx.Response.Close();
    }

    // --- Authorize + token calls ------------------------------------------------------------

    private static string BuildAuthorizeUrl(string challenge, string state, string redirectUri) =>
        $"{Authority}/oauth2/v2.0/authorize" +
        $"?client_id={ClientId}" +
        "&response_type=code&response_mode=query" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        $"&scope={Uri.EscapeDataString(Scope)}" +
        $"&state={state}" +
        $"&code_challenge={challenge}&code_challenge_method=S256";

    private static async Task<TokenResponse?> ExchangeCodeAsync(string code, string verifier, string redirectUri)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier,
            ["scope"] = Scope,
        });

        using var resp = await Http.PostAsync($"{Authority}/oauth2/v2.0/token", form);
        var json = await resp.Content.ReadAsStringAsync();

        TokenResponse? tokens = null;
        try { tokens = JsonSerializer.Deserialize(json, PlatformLoginJsonContext.Default.TokenResponse); }
        catch (JsonException) { /* non-JSON error body — handled below */ }

        if (!resp.IsSuccessStatusCode || string.IsNullOrEmpty(tokens?.AccessToken))
        {
            Console.Error.WriteLine($"Token exchange failed ({(int)resp.StatusCode}): {tokens?.Error ?? json}");
            if (tokens?.ErrorDescription is not null) Console.Error.WriteLine($"  {tokens.ErrorDescription}");
            return null;
        }

        return tokens;
    }

    // --- Helpers -----------------------------------------------------------------------------

    // Display label from the id_token payload. No signature check needed client-side: the token
    // arrived over the TLS channel we initiated (standard native-client posture).
    private static string ReadUserLabel(string? idToken)
    {
        if (idToken?.Split('.') is not [_, var payload, _]) return "(unknown user)";
        try
        {
            var claims = JsonSerializer.Deserialize(Base64Url.DecodeFromChars(payload), PlatformLoginJsonContext.Default.IdTokenClaims);
            return claims?.Email ?? claims?.PreferredUsername ?? claims?.Name ?? claims?.Sub ?? "(unknown user)";
        }
        catch (Exception e) when (e is JsonException or FormatException) { return "(unknown user)"; }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS()) Process.Start("open", url);
            else if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else Process.Start("xdg-open", url);
        }
        catch { /* the URL is printed as fallback */ }
    }
}

// --- Wire types ------------------------------------------------------------------------------

internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("id_token")] public string? IdToken { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
}

internal sealed class IdTokenClaims
{
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("preferred_username")] public string? PreferredUsername { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("sub")] public string? Sub { get; set; }
}

// Response from the issuance endpoint (ForgeUI POST /platform/keys), camelCase on the wire.
internal sealed class IssueKeyResponse
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("balanceMicroUsd")] public long BalanceMicroUsd { get; set; }
}

// Response from GET /me (ForgeUI), camelCase on the wire.
internal sealed class MeResponse
{
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    [JsonPropertyName("balanceMicroUsd")] public long BalanceMicroUsd { get; set; }
}

[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(IdTokenClaims))]
[JsonSerializable(typeof(IssueKeyResponse))]
[JsonSerializable(typeof(MeResponse))]
internal partial class PlatformLoginJsonContext : JsonSerializerContext { }
