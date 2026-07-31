using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.Services;

public sealed class WorkspaceState
{
    public WorkspaceState(string? initialRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(initialRoot))
            OpenFolder(initialRoot);
    }

    public IWorkspace? Workspace { get; private set; }

    public CapabilityRegistry? Capabilities { get; private set; }

    public string? Root => Workspace?.Roots[0];

    public void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Workspace = new LocalDiskWorkspace(path);
        Capabilities = new CapabilityRegistry(
        [
            new WorkspaceFileProvider(Workspace),
            new WorkspaceTerminalProvider(Workspace),
        ]);
    }
}
