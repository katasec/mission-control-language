using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ForgeMission.Tests.Integration;

/// <summary>
/// `forge claude` launcher (Phase 42.2): one command serves the mission on the Anthropic
/// wire, launches the real claude CLI wired to it, and tears everything down on exit.
/// CLI-level analogue of ClaudeCodeTests — asserts the answer AND that no orphan
/// server remains after the command returns.
/// </summary>
public sealed class ForgeClaudeLauncherTests
{
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task ForgeClaude_OneShotPrompt_AnswersAndTearsDown()
    {
        var apiKey = Environment.GetEnvironmentVariable("MCL_API_KEY");
        Skip.If(string.IsNullOrWhiteSpace(apiKey), "MCL_API_KEY not set");
        Skip.If(!IsOnPath("claude"),               "'claude' not found on PATH");

        // A mission dir with NO agent.yaml — exercises the lone-.mcl resolution path.
        var missionDir = CopyMissionToTemp("converse");

        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory       = missionDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        foreach (var arg in new[] { ForgeDllPath(), "claude", "-p", "Say exactly: forge works" })
            psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        var finished = await Task.Run(() => proc.WaitForExit(180_000));
        if (!finished) { proc.Kill(entireProcessTree: true); throw new TimeoutException("forge claude timed out"); }

        Assert.True(proc.ExitCode == 0, $"forge claude exited {proc.ExitCode}.\nSTDOUT: {stdout}\nSTDERR: {stderr}");
        Assert.Contains("forge works", stdout, StringComparison.OrdinalIgnoreCase);

        // Teardown proof: the ephemeral endpoint from the banner is no longer listening.
        var port = ParseEndpointPort(stderr);
        Assert.False(IsPortOpen(port), $"orphan server still listening on port {port} after forge claude exit");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static int ParseEndpointPort(string stderr)
    {
        var match = Regex.Match(stderr, @"http://127\.0\.0\.1:(\d+)");
        Assert.True(match.Success, $"no endpoint banner found in stderr:\n{stderr}");
        return int.Parse(match.Groups[1].Value);
    }

    private static bool IsPortOpen(int port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", port).Wait(500);
        }
        catch { return false; }
    }

    private static string ForgeDllPath()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory.Replace("ForgeMission.Tests", "ForgeMission.Cli"), "forge.dll");
        Assert.True(File.Exists(path), $"forge.dll not found at {path} — build ForgeMission.Cli first");
        return path;
    }

    private static string CopyMissionToTemp(string missionName)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Missions", missionName);
        var target = Path.Combine(Path.GetTempPath(), $"forge-launcher-{missionName}-{Guid.NewGuid():N}");
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest);
        }
        return target;
    }

    private static bool IsOnPath(string binary) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, binary))
                     || File.Exists(Path.Combine(dir, binary + ".exe")));
}
