namespace ForgeMission.Core.Tools;

public interface IWorkspace
{
    IReadOnlyList<string> Roots { get; }

    bool TryResolvePath(string path, out string resolved, out string? error);

    Task<bool> ExistsAsync(string resolvedPath, CancellationToken ct = default);
    Task<string> ReadFileAsync(string resolvedPath, CancellationToken ct = default);
    Task WriteFileAsync(string resolvedPath, string content, CancellationToken ct = default);

    Task<ToolExecutionResult> ExecuteAsync(
        string command,
        string? workingDir = null,
        CancellationToken ct = default);
}
