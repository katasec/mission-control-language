namespace ForgeMission.Core.Tools;

// Unrestricted execution — no allowlist, no confirmation gate, no sandbox (locked decision,
// phase-43.1). Safe to inherit the FULL parent environment (no explicit Environment scrubbing
// here — that's not an oversight, it's the point): Forge Desktop never sets the provider API
// key as a process environment variable anywhere, so there is nothing secret in the parent env
// for this child to inherit in the first place.
//
public sealed class BashToolExecutor : IToolExecutor
{
    public string Name => "Bash";

    public Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments, IWorkspace workspace, CancellationToken ct = default)
    {
        if (!ToolArguments.TryGetString(arguments, "command", out var command))
            return Task.FromResult(ToolExecutionResult.Error("command is required"));

        return workspace.ExecuteAsync(command, workingDir: null, ct);
    }
}
