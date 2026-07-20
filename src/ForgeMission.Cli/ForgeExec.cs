using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeMission.Core.Resolution;

namespace ForgeMission.Cli;

// forge exec (42.6 task 8): the one-shot top-of-funnel command — run a hosted mission once, stream
// live progress + the answer, exit. Sends the ExecuteMission message (API A) to ForgeAPI with the
// stored platform key. Message-based, not URL-shaped (M1/M2): the handle is a field on the request
// body, never a route segment. ClientToken is auto-generated per call (M7) so a retry never
// double-debits.
public static class ForgeExec
{
    // Same override convention as PlatformLogin's endpoints (FORGE_PLATFORM_ENDPOINT etc.) — the
    // platform-key issuer (ForgeUI) and the mission-invocation gateway (ForgeAPI) are different
    // hosts, so this needs its own var rather than reusing PlatformCredential.Endpoint.
    private static string ApiEndpoint =>
        Environment.GetEnvironmentVariable("FORGE_API_ENDPOINT")?.TrimEnd('/')
        ?? "https://api.forge.katasec.com";

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task<int> RunAsync(
        string target,
        string? prompt,
        string? inputPath = null,
        string? mode = null,
        string? outPath = null)
    {
        var platform = CredentialStore.GetPlatform();
        if (platform is null || string.IsNullOrEmpty(platform.Key))
        {
            Console.Error.WriteLine("Not signed in. Run `forge login`.");
            return 1;
        }

        var mission = target.StartsWith('@') ? target[1..] : target;
        var inputArtifacts = new List<MissionArtifactDto>();
        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            var upload = await UploadInputAsync(inputPath, platform.Key);
            if (upload is null) return 1;
            inputArtifacts.Add(upload);
        }

        if (string.IsNullOrWhiteSpace(prompt) && inputArtifacts.Count == 0)
        {
            Console.Error.WriteLine("exec failed: provide a prompt or --input <path>.");
            return 1;
        }

        var request = new ExecuteMissionRequest
        {
            Version = 1,
            ClientToken = Guid.NewGuid().ToString("N"),
            Mission = mission,
            Input = prompt ?? "",
            Inputs = Inputs(mode),
            InputArtifactIds = inputArtifacts.Select(a => a.Id).ToList(),
            Stream = true,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiEndpoint}/api/ExecuteMission")
        {
            Content = JsonContent.Create(request, ForgeExecJsonContext.Default.ExecuteMissionRequest),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", platform.Key);

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
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

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"exec failed ({(int)resp.StatusCode}): {errBody}");
            return 1;
        }

        var progress = new ProgressLine();
        ExecuteMissionResponseDto? result = null;

        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.Length == 0) continue;
            MissionRunEventDto? evt;
            try { evt = JsonSerializer.Deserialize(line, ForgeExecJsonContext.Default.MissionRunEventDto); }
            catch (JsonException) { continue; } // a malformed line shouldn't kill an otherwise-good stream

            switch (evt?.Type)
            {
                case "progress" when evt.Progress is not null:
                    progress.Update(Label(evt.Progress));
                    break;
                case "result" or "error":
                    result = evt.Result;
                    break;
            }
        }
        progress.Clear();

        if (result is null)
        {
            Console.Error.WriteLine("exec failed: the stream ended without a result.");
            return 1;
        }

        if (result.ResponseStatus?.ErrorCode is { Length: > 0 })
        {
            Console.Error.WriteLine($"exec failed [{result.ResponseStatus.ErrorCode}]: {result.ResponseStatus.Message}");
            return 1;
        }

        if (result.Artifacts is { Count: > 0 })
        {
            if (!await DownloadArtifactsAsync(result.Artifacts, platform.Key, inputPath, mode, outPath))
                return 1;
        }
        else
        {
            Console.WriteLine(result.Answer);
            Console.WriteLine();
        }
        Console.WriteLine(FormatFooter(result));
        return 0;
    }

    private static Dictionary<string, string>? Inputs(string? mode) =>
        string.IsNullOrWhiteSpace(mode)
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["mode"] = mode };

    private static async Task<MissionArtifactDto?> UploadInputAsync(string path, string platformKey)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"input file not found: {path}");
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var sha256 = await Sha256Async(fullPath);
        await using var file = File.OpenRead(fullPath);
        using var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(ContentTypeFor(fullPath));
        content.Headers.ContentLength = file.Length;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiEndpoint}/api/UploadArtifact")
        {
            Content = content,
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", platformKey);
        req.Headers.Add("X-Forge-Artifact-Name", Path.GetFileName(fullPath));
        req.Headers.Add("X-Forge-Artifact-Sha256", sha256);
        req.Headers.Add("X-Forge-Artifact-Client-Token", Guid.NewGuid().ToString("N"));

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        if (!resp.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"upload failed ({(int)resp.StatusCode}): {await resp.Content.ReadAsStringAsync()}");
            return null;
        }

        var upload = await resp.Content.ReadFromJsonAsync(
            ForgeExecJsonContext.Default.UploadArtifactResponseDto);
        if (upload?.ResponseStatus?.ErrorCode is { Length: > 0 })
        {
            Console.Error.WriteLine($"upload failed [{upload.ResponseStatus.ErrorCode}]: {upload.ResponseStatus.Message}");
            return null;
        }

        return upload?.Artifact;
    }

    private static async Task<bool> DownloadArtifactsAsync(
        List<MissionArtifactDto> artifacts,
        string platformKey,
        string? inputPath,
        string? mode,
        string? outPath)
    {
        for (var i = 0; i < artifacts.Count; i++)
        {
            var artifact = artifacts[i];
            var targetPath = i == 0 && !string.IsNullOrWhiteSpace(outPath)
                ? outPath!
                : i == 0 && !string.IsNullOrWhiteSpace(inputPath)
                    ? InferOutputPath(inputPath!, mode, artifact)
                    : artifact.Name;

            if (!await DownloadArtifactAsync(artifact.Id, targetPath, platformKey))
                return false;
            Console.WriteLine($"Created: {targetPath}");
        }

        Console.WriteLine();
        return true;
    }

    private static async Task<bool> DownloadArtifactAsync(string artifactId, string path, string platformKey)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiEndpoint}/api/GetArtifact")
        {
            Content = JsonContent.Create(
                new GetArtifactRequest { Version = 1, ArtifactId = artifactId },
                ForgeExecJsonContext.Default.GetArtifactRequest),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", platformKey);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        if (!resp.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"download failed ({(int)resp.StatusCode}): {await resp.Content.ReadAsStringAsync()}");
            return false;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        await using var input = await resp.Content.ReadAsStreamAsync();
        await using var output = File.Create(path);
        await input.CopyToAsync(output);
        return true;
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

    // Same human-label mapping as ForgeUI's RoomAgentInvoker.ProgressLabel — kept in sync by hand
    // (a shared-lib extraction wasn't worth it for one small pure function used by two hosts on
    // different sides of the AOT boundary). Provider-agnostic: the runner emits neutral kinds (41.7).
    private static string Label(MissionProgressDto p) => p.Kind switch
    {
        "searching_web" => p.Detail is { Length: > 0 } q ? $"Searching: “{Trim(q)}”{Count(p.ResultCount)}" : "Searching the web",
        "searching_x"   => p.Detail is { Length: > 0 } q ? $"Searching X: “{Trim(q)}”{Count(p.ResultCount)}" : "Searching X",
        "reading"       => p.Detail is { Length: > 0 } h ? $"Reading {h}" : "Reading a page",
        "results"       => p.ResultCount is int n ? $"Found {n} result{(n == 1 ? "" : "s")}" : "Reviewing results",
        "search"        => "Searching the web",
        "llm"           => "Thinking",
        "json_extract"  => "Routing",
        "http"          => "Fetching",
        "exec"          => "Running",
        "rule"          => "Checking",
        "onnx"          => "Classifying",
        _               => "Working",
    };

    private static string Trim(string s) => s.Length <= 48 ? s : s[..47] + "…";
    private static string Count(int? n) => n is int c and > 0 ? $" · {c} result{(c == 1 ? "" : "s")}" : "";

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    }

    private static string InferOutputPath(string inputPath, string? mode, MissionArtifactDto artifact)
    {
        var dir = Path.GetDirectoryName(inputPath);
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var inputExt = Path.GetExtension(inputPath).ToLowerInvariant();
        var ext = ExtensionFor(mode, artifact);
        var suffix = inputExt == ".pdf" && ext == ".pdf" ? ".ocr" : "";
        return Path.Combine(dir ?? "", $"{stem}{suffix}{ext}");
    }

    private static string ExtensionFor(string? mode, MissionArtifactDto artifact)
    {
        if (string.Equals(mode, "pdf", StringComparison.OrdinalIgnoreCase)
            || artifact.ContentType == "application/pdf")
            return ".pdf";
        if (artifact.ContentType == "application/json") return ".json";
        return ".txt";
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".txt" => "text/plain",
        ".pdf" => "application/pdf",
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };

    // Live-updating status line on a tty (carriage-return overwrite); falls back to one line per
    // change when stdout is redirected/piped, since \r is meaningless (and noisy) there.
    private sealed class ProgressLine
    {
        private int _lastLength;
        private readonly bool _interactive = !Console.IsOutputRedirected;

        public void Update(string label)
        {
            if (_interactive)
            {
                var padded = label.PadRight(_lastLength);
                Console.Write($"\r{padded}");
                _lastLength = label.Length;
            }
            else
            {
                Console.Error.WriteLine($"… {label}");
            }
        }

        public void Clear()
        {
            if (_interactive && _lastLength > 0)
                Console.Write($"\r{new string(' ', _lastLength)}\r");
        }
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
    public List<string>? InputArtifactIds { get; set; }
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
    public List<MissionArtifactDto>? Artifacts { get; set; }
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

internal sealed class UploadArtifactResponseDto
{
    public MissionArtifactDto? Artifact { get; set; }
    public ResponseStatusDto? ResponseStatus { get; set; }
}

internal sealed class GetArtifactRequest
{
    public int Version { get; set; }
    public string ArtifactId { get; set; } = "";
}

internal sealed class MissionArtifactDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Role { get; set; } = "";
}

// M10: streaming form. Type is progress | heartbeat | result | error.
internal sealed class MissionRunEventDto
{
    public string Type { get; set; } = "";
    public string RunId { get; set; } = "";
    public MissionProgressDto? Progress { get; set; }
    public ExecuteMissionResponseDto? Result { get; set; }
}

internal sealed class MissionProgressDto
{
    public string ExpertName { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Detail { get; set; }
    public int? ResultCount { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ExecuteMissionRequest))]
[JsonSerializable(typeof(ExecuteMissionResponseDto))]
[JsonSerializable(typeof(MissionRunEventDto))]
[JsonSerializable(typeof(UploadArtifactResponseDto))]
[JsonSerializable(typeof(GetArtifactRequest))]
internal partial class ForgeExecJsonContext : JsonSerializerContext { }
