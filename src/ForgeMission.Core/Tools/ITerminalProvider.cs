namespace ForgeMission.Core.Tools;

public interface ITerminalProvider : ICapabilityProvider
{
    Task<ToolExecutionResult> ExecuteAsync(
        string command,
        string? workingDir = null,
        CancellationToken ct = default);
}
