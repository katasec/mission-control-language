namespace ForgeMission.Core.Tools;

// Adapts the existing workspace's verified file behavior to the capability-provider contract.
// Confinement remains the workspace's responsibility; this wrapper deliberately adds no policy.
public sealed class WorkspaceFileProvider(IWorkspace workspace) : IFileProvider
{
    public string CapabilityName => "file";

    public IReadOnlyList<string> Roots => workspace.Roots;

    public bool TryResolvePath(string path, out string resolved, out string? error) =>
        workspace.TryResolvePath(path, out resolved, out error);

    public Task<bool> ExistsAsync(string resolvedPath, CancellationToken ct = default) =>
        workspace.ExistsAsync(resolvedPath, ct);

    public Task<string> ReadFileAsync(string resolvedPath, CancellationToken ct = default) =>
        workspace.ReadFileAsync(resolvedPath, ct);

    public Task WriteFileAsync(string resolvedPath, string content, CancellationToken ct = default) =>
        workspace.WriteFileAsync(resolvedPath, content, ct);
}
