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
    private const string ClientRuntimeUrl = "http://127.0.0.1:5001/";

    [Fact]
    public async Task StartsClientRuntimeWithTheResolvedDurableUrl()
    {
        var log = new List<string>();
        var client = new RecordingClientRuntime(log);

        var runtimes = await DesktopBoot.ComposeAsync(
            MissionRuntime(), DurableRuntime(), client.Start, CancellationToken.None);

        Assert.Equal(1, client.StartCount);
        Assert.Equal(DurableUrl, client.ConversationRuntimeBaseUrl);
        Assert.Equal(MissionRuntimeUrl, client.MissionRuntimeBaseUrl);
        Assert.Equal(MissionRuntimeMode, client.MissionRuntimeMode);
        Assert.Equal(ClientRuntimeUrl, runtimes.Url);
    }

    [Fact]
    public async Task NormalBoot_DisposesEachOwnedRuntimeExactlyOnce_InReverseOrder()
    {
        var log = new List<string>();
        var tunnel = new FakeTunnel(log);
        var launcher = new FakeMissionRuntimeLauncher(log);
        var client = new RecordingClientRuntime(log);

        var runtimes = await DesktopBoot.ComposeAsync(
            MissionRuntime(launcher), DurableRuntime(tunnel), client.Start, CancellationToken.None);

        Assert.Empty(log);

        await runtimes.DisposeAsync();

        Assert.Equal(["client", "conversation", "mission"], log);
        Assert.Equal(1, client.StopCount);
        Assert.Equal(1, tunnel.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    [Fact]
    public async Task DurableReadinessFailure_StartsNoClientRuntime_AndDisposesTheMissionLauncher()
    {
        var log = new List<string>();
        var launcher = new FakeMissionRuntimeLauncher(log);
        var client = new RecordingClientRuntime(log);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DesktopBoot.ComposeAsync(
            MissionRuntime(launcher),
            _ => throw new InvalidOperationException("Conversation Runtime did not become healthy."),
            client.Start,
            CancellationToken.None));

        Assert.Contains("did not become healthy", error.Message);
        Assert.Equal(0, client.StartCount);
        Assert.Equal(["mission"], log);
        Assert.Equal(1, launcher.DisposeCount);
    }

    // The child exists the moment Start returns, so a readiness wait that never succeeds must still
    // leave nothing running — this is the case an "await the ready URL, then take ownership" design
    // would silently orphan.
    [Fact]
    public async Task ClientRuntimeStartedThenReadinessFails_StopsTheStartedClientRuntimeThenLeaseThenLauncher_ExactlyOnce()
    {
        var log = new List<string>();
        var tunnel = new FakeTunnel(log);
        var launcher = new FakeMissionRuntimeLauncher(log);
        var client = new RecordingClientRuntime(log,
            Task.FromException<string>(new InvalidOperationException("Client Runtime did not start within 20s.")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DesktopBoot.ComposeAsync(
            MissionRuntime(launcher), DurableRuntime(tunnel), client.Start, CancellationToken.None));

        Assert.Contains("did not start within", error.Message);
        Assert.Equal(1, client.StartCount);
        Assert.Equal(["client", "conversation", "mission"], log);
        Assert.Equal(1, client.StopCount);
        Assert.Equal(1, tunnel.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    [Fact]
    public async Task ChildSpawnFailure_DisposesLeaseAndLauncherExactlyOnce_AndStopsNoClientRuntime()
    {
        var log = new List<string>();
        var tunnel = new FakeTunnel(log);
        var launcher = new FakeMissionRuntimeLauncher(log);

        await Assert.ThrowsAsync<FileNotFoundException>(() => DesktopBoot.ComposeAsync(
            MissionRuntime(launcher),
            DurableRuntime(tunnel),
            (_, _, _, _) => throw new FileNotFoundException("Could not find ForgeMission.ClientRuntime."),
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
        var client = new RecordingClientRuntime(log);

        await Assert.ThrowsAsync<InvalidOperationException>(() => DesktopBoot.ComposeAsync(
            _ => throw new InvalidOperationException("Docker prerequisite failed."),
            _ => { durablePrepared++; return Task.FromResult(new ConversationRuntimeLease(DurableUrl, null)); },
            client.Start,
            CancellationToken.None));

        Assert.Equal(0, durablePrepared);
        Assert.Equal(0, client.StartCount);
        Assert.Empty(log);
    }

    // The window can close between preparing the durable runtime and starting the child.
    [Fact]
    public async Task CancellationBeforeClientStart_StartsNoClientRuntime_AndDisposesWhatWasPrepared()
    {
        var log = new List<string>();
        var tunnel = new FakeTunnel(log);
        var launcher = new FakeMissionRuntimeLauncher(log);
        var client = new RecordingClientRuntime(log);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DesktopBoot.ComposeAsync(
            MissionRuntime(launcher),
            ct =>
            {
                cancellation.Cancel();
                return DurableRuntime(tunnel)(ct);
            },
            client.Start,
            cancellation.Token));

        Assert.Equal(0, client.StartCount);
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

    private sealed class RecordingClientRuntime(List<string> log, Task<string>? readyUrl = null)
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public string? MissionRuntimeBaseUrl { get; private set; }
        public string? MissionRuntimeMode { get; private set; }
        public string? ConversationRuntimeBaseUrl { get; private set; }

        public ClientRuntimeStart Start(
            string missionRuntimeBaseUrl, string missionRuntimeMode, string conversationRuntimeBaseUrl, CancellationToken ct)
        {
            StartCount++;
            MissionRuntimeBaseUrl = missionRuntimeBaseUrl;
            MissionRuntimeMode = missionRuntimeMode;
            ConversationRuntimeBaseUrl = conversationRuntimeBaseUrl;
            return new ClientRuntimeStart(readyUrl ?? Task.FromResult(ClientRuntimeUrl), StopAsync);
        }

        private ValueTask StopAsync()
        {
            StopCount++;
            log.Add("client");
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
