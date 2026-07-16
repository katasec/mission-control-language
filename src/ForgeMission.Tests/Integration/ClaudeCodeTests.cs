using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;

namespace ForgeMission.Tests.Integration;

public sealed class ClaudeCodeTests
{
    // ------------------------------------------------------------------
    // Live: Claude Code CLI → forge OaiServer → real LLM pipeline
    // ------------------------------------------------------------------
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task ClaudeCode_LiveRoundTrip_ThroughNoopMission()
    {
        var apiKey       = Environment.GetEnvironmentVariable("MCL_API_KEY");
        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Skip.If(string.IsNullOrWhiteSpace(apiKey),       "MCL_API_KEY not set");
        Skip.If(string.IsNullOrWhiteSpace(anthropicKey), "ANTHROPIC_API_KEY not set");
        Skip.If(!IsOnPath("claude"),                     "'claude' not found on PATH");

        var model    = Environment.GetEnvironmentVariable("MCL_MODEL") ?? "claude-3-5-haiku-20241022";
        // Use a direct IChatClient so AnthropicServer forwards the full conversation
        // history to the LLM. MissionChatClient only passes the last user message as
        // the mission goal, which breaks claude CLI's multi-turn internal reasoning.
        var chatClient = BuildDirectChatClient(apiKey!, model);

        await using var fixture = await AnthropicServerFixture.StartAsync(chatClient);

        var (exitCode, stdout, stderr) = await RunClaudeAsync(
            prompt:    "Say exactly: forge works",
            baseUrl:   fixture.BaseUrl,
            apiKey:    anthropicKey!,
            timeoutMs: 60_000);

        Assert.True(exitCode == 0, $"claude exited {exitCode}.\nSTDOUT: {stdout}\nSTDERR: {stderr}");
        var json  = JsonDocument.Parse(stdout).RootElement;
        var reply = json.GetProperty("result").GetString() ?? string.Empty;
        Assert.Contains("forge works", reply, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Live: Claude Code CLI → REAL `forge serve` (Anthropic wire) → mission (42.1)
    //
    // Two turns where turn 2 depends on turn 1 — provable only if the FULL
    // conversation reaches the mission (context["conversation"]), not just
    // the last user message. This is 42.1's "Done when".
    // ------------------------------------------------------------------
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task ClaudeCode_TwoTurn_ThroughForgeServe_AnthropicWire()
    {
        var apiKey = Environment.GetEnvironmentVariable("MCL_API_KEY");
        Skip.If(string.IsNullOrWhiteSpace(apiKey), "MCL_API_KEY not set");
        Skip.If(!IsOnPath("claude"),               "'claude' not found on PATH");

        var missionDir = CopyMissionToTemp("converse");
        var port       = FindFreePort();
        await File.WriteAllTextAsync(Path.Combine(missionDir, "agent.yaml"), $"""
            mission: mission.mcl
            id: converse
            port: {port}
            wire: anthropic
            """);

        using var serve = StartForgeServe(missionDir);
        try
        {
            await WaitUntilListeningAsync($"http://localhost:{port}/", timeoutMs: 30_000);

            var (exit1, stdout1, stderr1) = await RunClaudeAsync(
                prompt:    "Remember this secret word: PLATYPUS. Confirm briefly that you saved it.",
                baseUrl:   $"http://localhost:{port}",
                apiKey:    "dummy-forge-ignores-this",
                timeoutMs: 120_000);
            Assert.True(exit1 == 0, $"turn 1: claude exited {exit1}.\nSTDOUT: {stdout1}\nSTDERR: {stderr1}");
            var sessionId = JsonDocument.Parse(stdout1).RootElement.GetProperty("session_id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(sessionId), "turn 1 returned no session_id");

            var (exit2, stdout2, stderr2) = await RunClaudeAsync(
                prompt:    "What was the secret word I asked you to remember? Reply with just the word.",
                baseUrl:   $"http://localhost:{port}",
                apiKey:    "dummy-forge-ignores-this",
                timeoutMs: 120_000,
                resumeSessionId: sessionId);
            Assert.True(exit2 == 0, $"turn 2: claude exited {exit2}.\nSTDOUT: {stdout2}\nSTDERR: {stderr2}");

            var reply = JsonDocument.Parse(stdout2).RootElement.GetProperty("result").GetString() ?? string.Empty;
            Assert.Contains("PLATYPUS", reply, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!serve.HasExited) serve.Kill(entireProcessTree: true);
        }
    }

    // ------------------------------------------------------------------
    // Live: multi-tool task through forge serve — 42.3's Done-when (AUTHORITATIVE)
    //
    // The real claude CLI completes a tool-requiring task against a mission with a
    // tool-capable agent expert. Pass criterion is PLANTED tool-derived content (a magic
    // word only reachable by really reading the file) — never the CLI's status fields,
    // which report protocol success even when the tool loop is broken (probed 2026-07-16).
    // Also asserts enrich-once (counter file) and verify-runs (VERIFIED stamp).
    // ------------------------------------------------------------------
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task ClaudeCode_MultiToolTask_ThroughForgeServe_AgenticMission()
    {
        var apiKey = Environment.GetEnvironmentVariable("MCL_API_KEY");
        Skip.If(string.IsNullOrWhiteSpace(apiKey), "MCL_API_KEY not set");
        Skip.If(!IsOnPath("claude"),               "'claude' not found on PATH");

        // The plants: content the model cannot know, and an enrich-once counter.
        var magicWord = $"XYZZY-{Guid.NewGuid():N}";
        var plant     = Path.Combine(Path.GetTempPath(), $"forge-plant-{Guid.NewGuid():N}.txt");
        var counter   = Path.Combine(Path.GetTempPath(), $"forge-enrich-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(plant, $"the magic word is {magicWord}\n");

        var missionDir = CopyMissionToTemp("agentic");
        var port       = FindFreePort();
        await File.WriteAllTextAsync(Path.Combine(missionDir, "agent.yaml"), $"""
            mission: mission.mcl
            id: agentic
            port: {port}
            wire: anthropic
            """);

        using var serve = StartForgeServe(missionDir,
            new Dictionary<string, string> { ["FORGE_ENRICH_COUNTER"] = counter });
        try
        {
            await WaitUntilListeningAsync($"http://localhost:{port}/", timeoutMs: 30_000);

            var (exit, stdout, stderr) = await RunClaudeAsync(
                prompt:    $"Read the file {plant} and tell me the magic word.",
                baseUrl:   $"http://localhost:{port}",
                apiKey:    "dummy-forge-ignores-this",
                timeoutMs: 180_000,
                skipPermissions: true);   // -p mode denies file reads outside cwd otherwise
            Assert.True(exit == 0, $"claude exited {exit}.\nSTDOUT: {stdout}\nSTDERR: {stderr}");

            var reply = JsonDocument.Parse(stdout).RootElement.GetProperty("result").GetString() ?? string.Empty;

            // Planted content — only reachable via a real tool round-trip (no-false-green rule).
            Assert.Contains(magicWord, reply);
            // Post-agent ran on the final continuation (the regression this spoke exists to prevent).
            Assert.Contains("VERIFIED:", reply);
            // Pre-agent ran exactly once across the whole tool loop.
            var enrichRuns = (await File.ReadAllLinesAsync(counter)).Length;
            Assert.Equal(1, enrichRuns);
        }
        finally
        {
            if (!serve.HasExited) serve.Kill(entireProcessTree: true);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    // The CLI's build output, guaranteed fresh by the ReferenceOutputAssembly=false
    // ProjectReference: swap this test assembly's project segment for the CLI's.
    private static string ForgeDllPath()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory.Replace("ForgeMission.Tests", "ForgeMission.Cli"), "forge.dll");
        Assert.True(File.Exists(path), $"forge.dll not found at {path} — build ForgeMission.Cli first");
        return path;
    }

    private static Process StartForgeServe(string missionDir, IReadOnlyDictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo("dotnet", $"\"{ForgeDllPath()}\" serve agent.yaml")
        {
            WorkingDirectory       = missionDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        foreach (var (key, value) in env ?? new Dictionary<string, string>())
            psi.Environment[key] = value;
        var proc = Process.Start(psi)!;
        proc.OutputDataReceived += (_, _) => { };
        proc.ErrorDataReceived  += (_, _) => { };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        return proc;
    }

    private static async Task WaitUntilListeningAsync(string url, int timeoutMs)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { /* not up yet */ }
            await Task.Delay(250);
        }
        throw new TimeoutException($"forge serve did not start listening at {url} within {timeoutMs}ms");
    }

    private static string CopyMissionToTemp(string missionName)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Missions", missionName);
        var target = Path.Combine(Path.GetTempPath(), $"forge-test-{missionName}-{Guid.NewGuid():N}");
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest);
        }
        return target;
    }

    // Direct client preserves the full conversation history so claude CLI's
    // multi-turn internal reasoning (title generation, state tracking, etc.) works.
    private static IChatClient BuildDirectChatClient(string apiKey, string model) =>
        new OpenAIClient(new ApiKeyCredential(apiKey))
            .GetChatClient(model)
            .AsIChatClient();

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunClaudeAsync(
        string prompt,
        string baseUrl,
        string apiKey,
        int timeoutMs,
        string? resumeSessionId = null,
        bool skipPermissions = false)
    {
        var resume = resumeSessionId is null ? string.Empty : $"--resume {resumeSessionId} ";
        var perms  = skipPermissions ? "--dangerously-skip-permissions " : string.Empty;
        var psi = new ProcessStartInfo("claude", $"-p {resume}{perms}\"{prompt}\" --output-format json")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        // Redirect Claude Code's API calls to OaiServer instead of Anthropic
        psi.Environment["ANTHROPIC_BASE_URL"] = baseUrl;
        // claude validates that the key is present; forge ignores its value
        psi.Environment["ANTHROPIC_API_KEY"]  = apiKey;

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();

        var finished = await Task.Run(() => proc.WaitForExit(timeoutMs));
        if (!finished) { proc.Kill(); throw new TimeoutException("claude CLI timed out"); }

        return (proc.ExitCode, stdout, stderr);
    }

    private static int FindFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsOnPath(string binary) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, binary))
                     || File.Exists(Path.Combine(dir, binary + ".exe")));
}
