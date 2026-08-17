using ForgeMission.Desktop.Contracts;

namespace ForgeMission.Desktop;

internal enum DesktopState
{
    Booting,
    Ready,
    Failed,
    Stopping,
    Stopped,
}

// The Supervisor's lifecycle owner: the only place that decides what state the desktop is in, when
// the Host is told to navigate or show a failure, and when the runtimes are stopped. Everything slow
// happens here, off the Host's native thread and after the Host is already on screen.
//
// Three things end a run and all three converge on one exactly-once cleanup: the Host process exits
// (a normal window close and a crash are indistinguishable here, deliberately), a stop is signalled
// (SIGTERM/SIGINT), or a boot fails with no retry. A Host exit never runs cleanup itself — it is
// observed, and this class does the work.
internal sealed class DesktopLifecycle(IHostChannel host, Func<CancellationToken, Task<DesktopRuntimes>> bootAsync)
{
    private readonly Lock _cleanupGate = new();
    private CancellationTokenSource? _bootCancellation;
    private Task<DesktopRuntimes>? _boot;
    private Task? _cleanup;

    public DesktopState State { get; private set; } = DesktopState.Booting;

    public async Task RunAsync(CancellationToken stopRequested)
    {
        // Host first, before any potentially slow work: it renders its own Booting content without
        // needing a URL, a credential, or a runtime.
        await host.StartAsync(stopRequested);
        var hostExited = host.WaitForExitAsync();
        var stopSignalled = WhenCanceledAsync(stopRequested);

        while (true)
        {
            State = DesktopState.Booting;
            var (runtimes, failure) = await BootAsync(stopRequested, hostExited, stopSignalled);

            if (runtimes is not null)
            {
                State = DesktopState.Ready;
                await SendAsync(DesktopHostCommandKind.Navigate, runtimes.Url);
                await Task.WhenAny(hostExited, stopSignalled);
                break;
            }

            if (failure is null)
                break; // the Host went away or a stop was signalled mid-boot.

            State = DesktopState.Failed;
            await SendAsync(DesktopHostCommandKind.ShowFailure, failure);
            if (!await WaitForRetryAsync(hostExited, stopSignalled))
                break;
        }

        await CleanupAsync();
    }

    // Cleanup is idempotent and shared: every trigger awaits the same run, so the runtimes are
    // stopped exactly once no matter how many paths fire at once.
    public Task CleanupAsync()
    {
        lock (_cleanupGate)
        {
            return _cleanup ??= CleanupCoreAsync();
        }
    }

    private async Task<(DesktopRuntimes? Runtimes, string? Failure)> BootAsync(
        CancellationToken stopRequested, Task hostExited, Task stopSignalled)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopRequested);
        _bootCancellation = cancellation;
        var boot = bootAsync(cancellation.Token);
        _boot = boot;

        // Not awaiting the boot itself when something else finishes first: cleanup cancels it and
        // still disposes whatever it managed to start, so a window closed mid-boot leaves nothing.
        if (await Task.WhenAny(boot, hostExited, stopSignalled) != boot)
            return (null, null);

        try
        {
            return (await boot, null);
        }
        catch (OperationCanceledException)
        {
            return (null, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    // The only path back to Booting: the user clicked Retry in the Host's failure content.
    private async Task<bool> WaitForRetryAsync(Task hostExited, Task stopSignalled)
    {
        var retryRequested = host.ReadEventAsync(CancellationToken.None);
        if (await Task.WhenAny(retryRequested, hostExited, stopSignalled) != retryRequested)
        {
            Observe(retryRequested);
            return false;
        }

        try
        {
            return await retryRequested is { Kind: DesktopHostEventKind.RetryRequested };
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return false; // The Host is gone; its exit is what actually ends the run.
        }
    }

    private async Task CleanupCoreAsync()
    {
        State = DesktopState.Stopping;
        _bootCancellation?.Cancel();

        // Host first: on a signalled quit the window should go away immediately rather than after
        // the runtimes have finished stopping. On a window-close or crash it has already exited, so
        // the order costs nothing there.
        await host.DisposeAsync();
        await StopRuntimesAsync();
        State = DesktopState.Stopped;
    }

    // Awaits the boot rather than abandoning it: a Client Runtime or container that finished
    // starting just after the trigger must still be stopped. Boot honours its cancellation token,
    // so this settles quickly; correctness here outranks exit latency in a process whose window has
    // already gone.
    private async Task StopRuntimesAsync()
    {
        if (_boot is null)
            return;

        try
        {
            await (await _boot).DisposeAsync();
        }
        catch (Exception)
        {
            // A failed or cancelled boot already stopped whatever it had started.
        }
    }

    // A command sent to a Host that has just closed is not an error: the host-exit path is already
    // running and owns what happens next.
    private async Task SendAsync(DesktopHostCommandKind kind, string payload)
    {
        try
        {
            await host.SendAsync(new DesktopHostCommand(kind, payload), CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
    }

    private static Task WhenCanceledAsync(CancellationToken ct)
    {
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => canceled.TrySetResult());
        return canceled.Task;
    }

    // A pending pipe read faults when cleanup disposes the pipe; observing it keeps that expected
    // fault from surfacing as an unobserved task exception.
    private static void Observe(Task task) =>
        _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
}
