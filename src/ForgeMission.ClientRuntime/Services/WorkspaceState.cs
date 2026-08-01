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

    public ICapabilityDispatcher? Dispatcher { get; private set; }

    public ICapabilityAuditLog? AuditLog { get; private set; }

    public string? Root => Workspace?.Roots[0];

    public void OpenFolder(
        string path,
        ICapabilityAuthorizer? authorizer = null,
        ICapabilityConfirmationHandler? confirmationHandler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Workspace = new LocalDiskWorkspace(path);
        Capabilities = new CapabilityRegistry(
        [
            new WorkspaceFileProvider(Workspace),
            new WorkspaceTerminalProvider(Workspace),
        ]);
        AuditLog = new InMemoryCapabilityAuditLog();
        Dispatcher = new CapabilityDispatcher(
            Capabilities,
            authorizer ?? new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            AuditLog,
            confirmationHandler);
    }
}
