using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.Services;

public sealed class WorkspaceState
{
    public IWorkspace? Workspace { get; private set; }

    public string? Root => Workspace?.Roots[0];

    public void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Workspace = new LocalDiskWorkspace(path);
    }
}
