using System.Threading.Channels;
using ForgeMission.Desktop;
using ForgeMission.Desktop.Contracts;

namespace ForgeMission.Tests.Desktop;

// The behavioural half of the Supervisor/Host split: ordering (Host first, Navigate only when
// ready) and cleanup (exactly once, on every termination path, including a boot that finishes after
// the window is already gone). Verified against a fake host channel, so no native window is needed.
public sealed class DesktopLifecycleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task StartsHostBeforeBootWorkAndStaysBootingUntilRuntimesAreReady()
    {
        var host = new FakeHostChannel();
        var bootStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowBoot = new TaskCompletionSource<DesktopRuntimes>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostStartedFirst = false;

        var lifecycle = new DesktopLifecycle(host, _ =>
        {
            hostStartedFirst = host.Started;
            bootStarted.TrySetResult();
            return slowBoot.Task;
        });
        var run = lifecycle.RunAsync(CancellationToken.None);

        await bootStarted.Task.WaitAsync(Timeout);
        Assert.True(hostStartedFirst);
        Assert.Equal(DesktopState.Booting, lifecycle.State);
        Assert.Empty(host.Commands);

        var stopped = 0;
        slowBoot.SetResult(Runtimes("http://127.0.0.1:5001/", () => stopped++));
        await WaitUntilAsync(() => host.Commands.Count == 1);

        Assert.Equal(new DesktopHostCommand(DesktopHostCommandKind.Navigate, "http://127.0.0.1:5001/"), host.Commands[0]);
        Assert.Equal(DesktopState.Ready, lifecycle.State);

        host.Exit();
        await run.WaitAsync(Timeout);
        Assert.Equal(DesktopState.Stopped, lifecycle.State);
        Assert.Equal(1, stopped);
    }

    [Fact]
    public async Task HostExitStopsRuntimesExactlyOnceEvenWhenCleanupIsAlsoRequested()
    {
        var host = new FakeHostChannel();
        var stopped = 0;
        var lifecycle = new DesktopLifecycle(host, _ => Task.FromResult(Runtimes("http://127.0.0.1:5001/", () => stopped++)));
        var run = lifecycle.RunAsync(CancellationToken.None);

        await WaitUntilAsync(() => host.Commands.Count == 1);
        host.Exit();
        await Task.WhenAll(run, lifecycle.CleanupAsync(), lifecycle.CleanupAsync()).WaitAsync(Timeout);

        Assert.Equal(1, stopped);
        Assert.Equal(1, host.DisposeCount);
        Assert.Equal(DesktopState.Stopped, lifecycle.State);
    }

    // The window can close while Docker or the Client Runtime is still starting. Whatever that boot
    // finishes producing must still be stopped, or it outlives the app.
    [Fact]
    public async Task HostExitDuringBootCancelsBootAndStillStopsWhatItStarted()
    {
        var host = new FakeHostChannel();
        var bootStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = 0;
        var bootCanceled = false;

        var lifecycle = new DesktopLifecycle(host, async ct =>
        {
            bootStarted.TrySetResult();
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                bootCanceled = true;
            }

            // A runtime that came up just as the window went away.
            return Runtimes("http://127.0.0.1:5001/", () => stopped++);
        });
        var run = lifecycle.RunAsync(CancellationToken.None);

        await bootStarted.Task.WaitAsync(Timeout);
        host.Exit();
        await run.WaitAsync(Timeout);

        Assert.True(bootCanceled);
        Assert.Equal(1, stopped);
        Assert.Empty(host.Commands);
        Assert.Equal(DesktopState.Stopped, lifecycle.State);
    }

    [Fact]
    public async Task BootFailureShowsFailureAndOnlyRetryBootsAgain()
    {
        var host = new FakeHostChannel();
        var attempts = 0;
        var lifecycle = new DesktopLifecycle(host, _ =>
            ++attempts == 1
                ? Task.FromException<DesktopRuntimes>(new InvalidOperationException("Not signed in. Run `forge login`, then retry."))
                : Task.FromResult(Runtimes("http://127.0.0.1:5002/", () => { })));
        var run = lifecycle.RunAsync(CancellationToken.None);

        await WaitUntilAsync(() => host.Commands.Count == 1);
        Assert.Equal(DesktopHostCommandKind.ShowFailure, host.Commands[0].Kind);
        Assert.Contains("forge login", host.Commands[0].Payload);
        Assert.Equal(DesktopState.Failed, lifecycle.State);
        Assert.Equal(1, attempts);

        host.RequestRetry();
        await WaitUntilAsync(() => host.Commands.Count == 2);

        Assert.Equal(new DesktopHostCommand(DesktopHostCommandKind.Navigate, "http://127.0.0.1:5002/"), host.Commands[1]);
        Assert.Equal(2, attempts);

        host.Exit();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task StopSignalStopsRuntimesAndTerminatesHost()
    {
        var host = new FakeHostChannel();
        var stopped = 0;
        using var stopRequested = new CancellationTokenSource();
        var lifecycle = new DesktopLifecycle(host, _ => Task.FromResult(Runtimes("http://127.0.0.1:5001/", () => stopped++)));
        var run = lifecycle.RunAsync(stopRequested.Token);

        await WaitUntilAsync(() => host.Commands.Count == 1);
        stopRequested.Cancel();
        await run.WaitAsync(Timeout);

        Assert.Equal(1, stopped);
        Assert.Equal(1, host.DisposeCount);
        Assert.Equal(DesktopState.Stopped, lifecycle.State);
    }

    private static DesktopRuntimes Runtimes(string url, Action onStop) =>
        new(url, () =>
        {
            onStop();
            return ValueTask.CompletedTask;
        });

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        Assert.Fail("Timed out waiting for the expected lifecycle state.");
    }

    private sealed class FakeHostChannel : IHostChannel
    {
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Channel<DesktopHostEvent> _events = Channel.CreateUnbounded<DesktopHostEvent>();

        public bool Started { get; private set; }

        public List<DesktopHostCommand> Commands { get; } = [];

        public int DisposeCount { get; private set; }

        public Task StartAsync(CancellationToken ct)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task SendAsync(DesktopHostCommand command, CancellationToken ct)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public async Task<DesktopHostEvent?> ReadEventAsync(CancellationToken ct) =>
            await _events.Reader.ReadAsync(ct);

        public Task WaitForExitAsync() => _exited.Task;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public void Exit() => _exited.TrySetResult();

        public void RequestRetry() => _events.Writer.TryWrite(new DesktopHostEvent(DesktopHostEventKind.RetryRequested));
    }
}
