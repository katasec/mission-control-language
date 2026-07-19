using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeMission.Core.Resolution;

namespace ForgeMission.Cli;

// forge exec (42.6 task 8): the one-shot top-of-funnel command — run a hosted mission once, stream
// the answer, exit. Sends the ExecuteMission message (API A) to ForgeAPI with the stored platform
// key. Message-based, not URL-shaped (M1/M2): the handle is a field on the request body, never a
// route segment. ClientToken is auto-generated per call (M7) so a retry never double-debits.
public static class ForgeExec
{
    // Same override convention as PlatformLogin's endpoints (FORGE_PLATFORM_ENDPOINT etc.) — the
    // platform-key issuer (ForgeUI) and the mission-invocation gateway (ForgeAPI) are different
    // hosts, so this needs its own var rather than reusing PlatformCredential.Endpoint.
    private static string ApiEndpoint =>
        Environment.GetEnvironmentVariable("FORGE_API_ENDPOINT")?.TrimEnd('/')
        ?? "https://api.forge.katasec.com";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static async Task<int> RunAsync(string target, string prompt)
    {
        var platform = CredentialStore.GetPlatform();
        if (platform is null || string.IsNullOrEmpty(platform.Key))
        {
            Console.Error.WriteLine("Not signed in. Run `forge login`.");
            return 1;
        }

        var mission = target.StartsWith('@') ? target[1..] : target;
        var request = new ExecuteMissionRequest
        {
            Version = 1,
            ClientToken = Guid.NewGuid().ToString("N"),
            Mission = mission,
            Input = prompt,
            Stream = false,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiEndpoint}/api/ExecuteMission")
        {
            Content = JsonContent.Create(request, ForgeExecJsonContext.Default.ExecuteMissionRequest),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", platform.Key);

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine($"Could not reach {ApiEndpoint}: {ex.Message}");
            return 1;
        }

        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.Error.WriteLine("Your platform key is no longer valid (expired or revoked). Run `forge login`.");
            return 1;
        }

        var body = await resp.Content.ReadAsStringAsync();
        ExecuteMissionResponseDto? result;
        try
        {
            result = JsonSerializer.Deserialize(body, ForgeExecJsonContext.Default.ExecuteMissionResponseDto);
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"exec failed ({(int)resp.StatusCode}) — unreadable response from {ApiEndpoint}: {body}");
            return 1;
        }

        if (result is null || result.ResponseStatus?.ErrorCode is { Length: > 0 } errorCode)
        {
            var message = result?.ResponseStatus?.Message ?? body;
            Console.Error.WriteLine($"exec failed [{result?.ResponseStatus?.ErrorCode ?? "Unknown"}]: {message}");
            return 1;
        }

        Console.WriteLine(result.Answer);
        Console.WriteLine();
        Console.WriteLine(FormatFooter(result));
        return 0;
    }

    // UX decision (2026-07-17, phase-42.6 spoke): a trust footer, not a receipt — no cost/balance
    // per call (pull-not-push; `forge whoami` is the pull). Source count is omitted while
    // MissionSource[] stays empty (the runner contract carries no structured citations yet — the
    // "Known gap" in the spoke) rather than print a misleading "0 sources" under a verified answer
    // that already has inline citations in the text.
    private static string FormatFooter(ExecuteMissionResponseDto result)
    {
        var badge = result.Verified ? "✓ verified" : "⚠ unverified";
        return result.Sources is { Count: > 0 }
            ? $"{badge} · {result.Sources.Count} source(s) (--sources to expand)"
            : badge;
    }
}

// --- Wire types (mirrors ForgeMission.Api/Messages.cs — a client, not a shared-lib reference,
// since ForgeAPI is a non-AOT server project the AOT `forge` CLI must not depend on) -------------

internal sealed class ExecuteMissionRequest
{
    public int Version { get; set; }
    public string ClientToken { get; set; } = "";
    public string Mission { get; set; } = "";
    public string? MissionVersion { get; set; }
    public string Input { get; set; } = "";
    public Dictionary<string, string>? Inputs { get; set; }
    public bool Stream { get; set; }
}

internal sealed class ExecuteMissionResponseDto
{
    public string RunId { get; set; } = "";
    public string Mission { get; set; } = "";
    public string MissionVersion { get; set; } = "";
    public string Answer { get; set; } = "";
    public bool Verified { get; set; }
    public List<MissionSourceDto>? Sources { get; set; }
    public ResponseStatusDto? ResponseStatus { get; set; }
}

internal sealed class MissionSourceDto
{
    public string Url { get; set; } = "";
    public string? Title { get; set; }
    public string Provider { get; set; } = "";
}

internal sealed class ResponseStatusDto
{
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ExecuteMissionRequest))]
[JsonSerializable(typeof(ExecuteMissionResponseDto))]
internal partial class ForgeExecJsonContext : JsonSerializerContext { }
