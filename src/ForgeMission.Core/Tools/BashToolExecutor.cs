using System.Diagnostics;

namespace ForgeMission.Core.Tools;

// Unrestricted execution — no allowlist, no confirmation gate, no sandbox (locked decision,
// phase-43.1). Safe to inherit the FULL parent environment (no explicit Environment scrubbing
// here — that's not an oversight, it's the point): Forge Desktop never sets the provider API
// key as a process environment variable anywhere, so there is nothing secret in the parent env
// for this child to inherit in the first place.
//
// The default timeout is a hang-safety net, not a command restriction — a runaway or
// interactive-waiting process would otherwise block the agentic loop forever with no way to
// recover short of killing the whole app.
public sealed class BashToolExecutor(TimeSpan? timeout = null) : IToolExecutor
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(5);

    public string Name => "Bash";

    public async Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments, string workspaceRoot, CancellationToken ct = default)
    {
        if (!ToolArguments.TryGetString(arguments, "command", out var command))
            return ToolExecutionResult.Error("command is required");

        var psi = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh")
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            WorkingDirectory       = workspaceRoot,
        };
        psi.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        psi.ArgumentList.Add(command);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Error($"Failed to start command: {ex.Message}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return ToolExecutionResult.Error($"Command timed out after {_timeout.TotalSeconds:0}s: {command}");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var combined = string.Join('\n', new[] { stdout, stderr }.Where(s => !string.IsNullOrEmpty(s)));

        return process.ExitCode == 0
            ? new ToolExecutionResult(combined)
            : ToolExecutionResult.Error($"Command exited with code {process.ExitCode}\n{combined}".TrimEnd());
    }
}
