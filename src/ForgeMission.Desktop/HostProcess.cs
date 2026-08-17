using System.Diagnostics;
using System.IO.Pipes;
using ForgeMission.Desktop.Contracts;

namespace ForgeMission.Desktop;

// The Supervisor's view of the native host: start it, send it the two fixed commands, hear its one
// event, know when it exits, and be able to terminate it. Nothing here names or knows a concrete
// host — that is a separate executable found by path.
//
// This exists as an interface for one reason: DesktopLifecycle's ordering and cleanup rules are
// verified against a fake channel in DesktopLifecycleTests without starting a real window.
internal interface IHostChannel : IAsyncDisposable
{
    Task StartAsync(CancellationToken ct);

    Task SendAsync(DesktopHostCommand command, CancellationToken ct);

    Task<DesktopHostEvent?> ReadEventAsync(CancellationToken ct);

    // Completes when the host process exits — a normal window close and a crash look the same here,
    // which is the point: the Supervisor reacts to the process, not to a native callback.
    Task WaitForExitAsync();
}

internal sealed class HostProcess : IHostChannel
{
    private const string HostProjectName = "ForgeMission.Desktop.Host";

    private AnonymousPipeServerStream? _commands;
    private AnonymousPipeServerStream? _events;
    private Process? _process;

    public Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _commands = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        _events = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);

        var (fileName, dllArgument) = SiblingExecutable.Resolve(HostProjectName);
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
        if (dllArgument is not null)
            startInfo.ArgumentList.Add(dllArgument);
        startInfo.ArgumentList.Add("--command-pipe");
        startInfo.ArgumentList.Add(_commands.GetClientHandleAsString());
        startInfo.ArgumentList.Add("--event-pipe");
        startInfo.ArgumentList.Add(_events.GetClientHandleAsString());

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.Start();

        // Must happen after the child has inherited them, or this end never sees the pipe close.
        _commands.DisposeLocalCopyOfClientHandle();
        _events.DisposeLocalCopyOfClientHandle();
        return Task.CompletedTask;
    }

    public Task SendAsync(DesktopHostCommand command, CancellationToken ct) =>
        DesktopHostProtocol.WriteAsync(Started(_commands), command, ct);

    public Task<DesktopHostEvent?> ReadEventAsync(CancellationToken ct) =>
        DesktopHostProtocol.ReadEventAsync(Started(_events), ct);

    public Task WaitForExitAsync() => Started(_process).WaitForExitAsync();

    // Terminating the Host is part of the Supervisor's own shutdown; the reverse — a Host exit
    // owning Supervisor cleanup — is exactly what this process split exists to prevent.
    public ValueTask DisposeAsync()
    {
        if (_process is { } process)
        {
            ProcessTermination.Kill(process);
            process.Dispose();
            _process = null;
        }

        _commands?.Dispose();
        _events?.Dispose();
        _commands = null;
        _events = null;
        return ValueTask.CompletedTask;
    }

    private static T Started<T>(T? member) where T : class =>
        member ?? throw new InvalidOperationException("The desktop host process has not been started.");
}
