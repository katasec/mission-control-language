using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.ClientRuntime;

public sealed class ClientExecutionSession : ICapabilityDispatcher, IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime;
    private readonly CapabilityDispatcher _dispatcher;
    private readonly object _gate = new();
    private Task _drain = Task.CompletedTask;
    private Task? _dispose;
    private bool _closed;

    private ClientExecutionSession(string root, CapabilityAuthorizationPolicy policy,
        ICapabilityConfirmationHandler confirmation, CancellationToken lifetime)
    {
        Root = root;
        var workspace = new LocalDiskWorkspace(root);
        Capabilities = new CapabilityRegistry([
            new WorkspaceFileProvider(workspace),
            new WorkspaceTerminalProvider(workspace),
        ]);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        _dispatcher = new CapabilityDispatcher(Capabilities, new PolicyCapabilityAuthorizer(policy),
            new InMemoryCapabilityAuditLog(), confirmation);
        AvailableCapabilities = Capabilities.AvailableCapabilities;
        ToolDeclarations = Capabilities.ToolDeclarations;
    }

    public IReadOnlyList<string> AvailableCapabilities { get; }
    public IReadOnlyList<AITool> ToolDeclarations { get; }

    internal string Root { get; }
    internal CapabilityRegistry Capabilities { get; }

    public static ClientExecutionSession Create(string root, CapabilityAuthorizationPolicy policy,
        ICapabilityConfirmationHandler confirmation, CancellationToken lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(confirmation);
        return new ClientExecutionSession(root, policy, confirmation, lifetime);
    }

    public Task<ToolExecutionResult> DispatchAsync(string capabilityName, object request, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_closed)
                return Task.FromResult(new ToolExecutionResult("This execution session is closed.", IsError: true));

            var admitted = DispatchAdmittedAsync(capabilityName, request, ct);
            _drain = Task.WhenAll(_drain, admitted);
            return admitted;
        }
    }

    private async Task<ToolExecutionResult> DispatchAdmittedAsync(string capabilityName, object request, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        return await _dispatcher.DispatchAsync(capabilityName, request, linked.Token);
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _dispose ??= DisposeCoreAsync();
            return new ValueTask(_dispose);
        }
    }

    private async Task DisposeCoreAsync()
    {
        Task drain;
        lock (_gate)
        {
            _closed = true;
            _lifetime.Cancel();
            drain = _drain;
        }
        try { await drain; }
        catch (OperationCanceledException) { }
        finally { _lifetime.Dispose(); }
    }
}
