namespace ForgeMission.Core.Tools;

// Adapts the existing workspace's verified terminal behavior to the capability-provider contract.
// The workspace keeps ownership of its unrestricted shell, environment inheritance, and timeout.
public sealed class WorkspaceTerminalProvider(IWorkspace workspace) : ITerminalProvider
{
    public string CapabilityName => "terminal";

    public Task<ToolExecutionResult> ExecuteAsync(
        string command,
        string? workingDir = null,
        CancellationToken ct = default) =>
        workspace.ExecuteAsync(command, workingDir, ct);
}
