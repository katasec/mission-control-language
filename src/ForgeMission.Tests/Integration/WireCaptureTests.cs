using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Katasec.AnthropicServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ForgeMission.Tests.Integration;

/// <summary>
/// Wire-capture harness (Phase 42.3 task 0). Captures what the REAL `claude` CLI sends on the wire so
/// the 42.3 classifier / goal-extraction / canonicalization rules rest on observation, not inference.
/// Re-run on every CLI version bump; diff against the checked-in fixtures (sanitize before check-in —
/// the raw capture contains metadata ids, memory contents, and connected MCP tool names).
/// Findings from the 2026-07-16 run are recorded in docs/phases/phase-42.3 §0/§2/§3 and 42.1.
/// </summary>
public sealed class WireCaptureTests
{
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Capture_RealClaudeCli_RequestBodies()
    {
        var apiKey = Environment.GetEnvironmentVariable("MCL_API_KEY");
        Skip.If(string.IsNullOrWhiteSpace(apiKey), "MCL_API_KEY not set");
        Skip.If(!IsOnPath("claude"), "'claude' not found on PATH");

        var capturePath = Environment.GetEnvironmentVariable("FORGE_CAPTURE_PATH")
                          ?? Path.Combine(Path.GetTempPath(), "forge-wire-capture.txt");
        if (File.Exists(capturePath)) File.Delete(capturePath);

        var model      = Environment.GetEnvironmentVariable("MCL_MODEL") ?? "gpt-4o-mini";
        var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey!))
            .GetChatClient(model)
            .AsIChatClient();

        var port    = FindFreePort();
        var server  = new AnthropicServer(chatClient, "forge");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting("urls", $"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        var callNo = 0;
        app.Use(async (ctx, next) =>
        {
            ctx.Request.EnableBuffering();
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            ctx.Request.Body.Position = 0;
            var n = Interlocked.Increment(ref callNo);
            await File.AppendAllTextAsync(capturePath,
                $"########## CALL {n}  {ctx.Request.Method} {ctx.Request.Path}\n{body}\n\n");
            await next();
        });

        server.Map(app);
        await app.StartAsync();

        try
        {
            // A prompt that REQUIRES a tool: claude must Read the file to answer.
            var probe = Path.Combine(Path.GetTempPath(), "forge-probe.txt");
            await File.WriteAllTextAsync(probe, "the magic word is PLATYPUS\n");

            var (exitCode, stdout, stderr) = await RunClaudeAsync(
                prompt:    $"Read the file {probe} and tell me the magic word.",
                baseUrl:   $"http://localhost:{port}",
                timeoutMs: 120_000);

            await File.AppendAllTextAsync(capturePath,
                $"\n########## CLAUDE EXIT {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}\n");
        }
        finally
        {
            await app.StopAsync();
        }

        Assert.True(File.Exists(capturePath), "no capture written");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunClaudeAsync(
        string prompt, string baseUrl, int timeoutMs)
    {
        var psi = new ProcessStartInfo("claude", $"-p \"{prompt}\" --output-format json")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        psi.Environment["ANTHROPIC_BASE_URL"] = baseUrl;
        psi.Environment["ANTHROPIC_API_KEY"]  = "dummy-forge-ignores-this";

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        var finished = await Task.Run(() => proc.WaitForExit(timeoutMs));
        if (!finished) { proc.Kill(); throw new TimeoutException("claude CLI timed out"); }
        return (proc.ExitCode, stdout, stderr);
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsOnPath(string binary) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, binary))
                     || File.Exists(Path.Combine(dir, binary + ".exe")));
}
