using ForgeMission.Desktop;
using ForgeMission.Orchestration;

namespace ForgeMission.Tests.Desktop;

// The Supervisor's startup contract: both runtimes are prepared and verified before the child that
// depends on them exists, the child is handed the verified durable URL, and every failure path stops
// exactly what that boot started, in reverse dependency order. Driven through DesktopBoot's own
// seams, so no Docker, Kind, credential, or real child process is involved.
public sealed class DesktopBootTests
{
    private const string MissionRuntimeUrl = "https://forge.katasec.com/";
    private const string MissionRuntimeMode = "cloud";
    private const string DurableUrl = "http://127.0.0.1:18080/";
    private const string ApplicationHostUrl = "http://127.0.0.1:5001/";

    [Fact]
    public async Task StartsApplicationHostWithTheResolvedDurableUrl()
    {
        var log = new List<string>();
        var applicationHost = new RecordingApplicationHost(log);

        var runtimes = await DesktopBoot.ComposeAsync(
            MissionRuntime(), DurableRuntime(), applicationHost.Start, CancellationToken.None);

        Assert.Equal(1, applicationHost.StartCount);
        Assert.Equal(DurableUrl, applicationHost.ConversationRuntimeBaseUrl);
        Assert.Equal(MissionRuntimeUrl, applicationHost.MissionRuntimeBaseUrl);
        Assert.Equal(MissionRuntimeMode, applicationHost.MissionRuntimeMode);
        Assert.Equal(ApplicationHostUrl, runtimes.Url);
    }

    [Fact]
    public async Task NormalBoot_DisposesEachOwnedRuntimeExactlyOnce_InReverseOrder()
    {
        var log = new List<string>();
        var tunnel = new FakeTunnel(log);
        var launcher = new FakeMissionRuntimeLauncher(log);
        var applicationHost = new RecordingApplicationHost(log);

        var runtimes = await DesktopBoot.ComposeAsync(
            MissionRuntime(launcher), DurableRuntime(tunnel), applicationHost.Start, CancellationToken.None);

        Assert.Empty(log);

        await runtimes.DisposeAsync();

        Assert.Equal(["application-host", "conversation", "mission"], log);
        Assert.Equal(1, applicationHost.StopCount);
        Assert.Equal(1, tunnel.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    [Fact]
    public async Task DurableReadinessFailure_StartsNoApplicationHost_AndDisposesTheMissionLauncher()
    {
        var log = new List<string>();
        var launcher = new FakeMissionRuntimeLauncher(log);
        var applicationHost = new RecordingApplicationHost(log);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DesktopBoot.ComposeAsync(
            MissionRuntime(launcher),
            _ => throw new InvalidOperationException("Conversation Runtime did not become healthy."),
            applicationHost.Start,
            CancellationToken.None));

        Assert.Contains("did not become healthy", error.Message);
        Assert.Equal(0, applicationHost.StartCount);
        Assert.Equal(["mission"], log);
        Assert.Equal(1, launcher.DisposeCount);
    }

    // The child exists the moment Start returns, so a readiness wait that never succeeds must still
    // leave nothing running — this is the case an "await the ready URL, then take ownership" design
    // would silently orphan.
    [Fact]
    public async Task ApplicationHostStartedThenReadinessFails_StopsTheStartedApplicationHostThenLeaseThenLauncher_ExactlyOnce()
    {
        var log = new List<string>();
        var tunnel = new FakeTunnel(log);
        var launcher = new FakeMissionRuntimeLauncher(log);
        var applicationHost = new RecordingApplicationHost(log,
            Task.FromException<string>(new InvalidOperationException("Application Host did not start within 20s.")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DesktopBoot.ComposeAsync(
            MissionRuntime(launcher), DurableRuntime(tunnel), applicationHost.Start, CancellationToken.None));

        Assert.Contains("did not start within", error.Message);
        Assert.Equal(1, applicationHost.StartCount);
        Assert.Equal(["application-host", "conversation", "mission"], log);
        Assert.Equal(1, applicationHost.StopCount);
        Assert.Equal(1, tunnel.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    [Fact]
    public async Task ChildSpawnFailure_DisposesLeaseAndLauncherExactlyOnce_AndStopsNoApplicationHost()
    {
        var log = new List<string>();
        var tunnel = new FakeTunnel(log);
        var launcher = new FakeMissionRuntimeLauncher(log);

        await Assert.ThrowsAsync<FileNotFoundException>(() => DesktopBoot.ComposeAsync(
            MissionRuntime(launcher),
            DurableRuntime(tunnel),
            (_, _, _, _) => throw new FileNotFoundException("Could not find ForgeMission.Application.Host."),
            CancellationToken.None));

        Assert.Equal(["conversation", "mission"], log);
        Assert.Equal(1, tunnel.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    [Fact]
    public async Task MissionRuntimeFailure_DisposesNothing_AndNeverPreparesTheDurableRuntime()
    {
        var log = new List<string>();
        var durablePrepared = 0;
        var applicationHost = new RecordingApplicationHost(log);

        await Assert.ThrowsAsync<InvalidOperationException>(() => DesktopBoot.ComposeAsync(
            _ => throw new InvalidOperationException("Docker prerequisite failed."),
            _ => { durablePrepared++; return Task.FromResult(new ConversationRuntimeLease(DurableUrl, null)); },
            applicationHost.Start,
            CancellationToken.None));

        Assert.Equal(0, durablePrepared);
        Assert.Equal(0, applicationHost.StartCount);
        Assert.Empty(log);
    }

    // The window can close between preparing the durable runtime and starting the child.
    [Fact]
    public async Task CancellationBeforeApplicationHostStart_StartsNoApplicationHost_AndDisposesWhatWasPrepared()
    {
        var log = new List<string>();
        var tunnel = new FakeTunnel(log);
        var launcher = new FakeMissionRuntimeLauncher(log);
        var applicationHost = new RecordingApplicationHost(log);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DesktopBoot.ComposeAsync(
            MissionRuntime(launcher),
            ct =>
            {
                cancellation.Cancel();
                return DurableRuntime(tunnel)(ct);
            },
            applicationHost.Start,
            cancellation.Token));

        Assert.Equal(0, applicationHost.StartCount);
        Assert.Equal(["conversation", "mission"], log);
        Assert.Equal(1, tunnel.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    private static Func<CancellationToken, Task<(string, string, IMissionRuntimeLauncher?)>> MissionRuntime(
        FakeMissionRuntimeLauncher? launcher = null) =>
        _ => Task.FromResult<(string, string, IMissionRuntimeLauncher?)>(
            (MissionRuntimeUrl, MissionRuntimeMode, launcher));

    private static Func<CancellationToken, Task<ConversationRuntimeLease>> DurableRuntime(
        FakeTunnel? tunnel = null) =>
        _ => Task.FromResult(new ConversationRuntimeLease(DurableUrl, tunnel));

    private sealed class RecordingApplicationHost(List<string> log, Task<string>? readyUrl = null)
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public string? MissionRuntimeBaseUrl { get; private set; }
        public string? MissionRuntimeMode { get; private set; }
        public string? ConversationRuntimeBaseUrl { get; private set; }

        public ApplicationHostStart Start(
            string missionRuntimeBaseUrl, string missionRuntimeMode, string conversationRuntimeBaseUrl, CancellationToken ct)
        {
            StartCount++;
            MissionRuntimeBaseUrl = missionRuntimeBaseUrl;
            MissionRuntimeMode = missionRuntimeMode;
            ConversationRuntimeBaseUrl = conversationRuntimeBaseUrl;
            return new ApplicationHostStart(readyUrl ?? Task.FromResult(ApplicationHostUrl), StopAsync);
        }

        private ValueTask StopAsync()
        {
            StopCount++;
            log.Add("application-host");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTunnel(List<string> log) : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            log.Add("conversation");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeMissionRuntimeLauncher(List<string> log) : IMissionRuntimeLauncher
    {
        public int DisposeCount { get; private set; }

        public string BaseUrl => MissionRuntimeUrl;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            log.Add("mission");
            return ValueTask.CompletedTask;
        }
    }
}
