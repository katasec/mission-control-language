using System.Diagnostics;
using ForgeMission.Core.Resolution;
using ForgeMission.Orchestration;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Desktop;

// What a successful boot produced: the URL the Host should navigate to, and the one operation that
// stops whatever that boot started. The Supervisor's DesktopLifecycle owns when this is disposed;
// nothing else may dispose it.
internal sealed class DesktopRuntimes(string url, Func<ValueTask> stopAsync) : IAsyncDisposable
{
    public string Url { get; } = url;

    public ValueTask DisposeAsync() => stopAsync();
}

// The potentially slow work the Supervisor performs while the Host is already on screen: resolve
// where the Mission Runtime lives (Orchestration's decision to carry out, never the Desktop's or
// the Client Runtime's), then start the Client Runtime and wait for its ready URL.
//
// A boot either returns fully-started runtimes or throws having stopped whatever it partially
// started — the lifecycle never inherits a half-built runtime set.
internal static class DesktopBoot
{
    // Dev/test convenience: point at a Client Runtime already running elsewhere. Nothing to start,
    // so nothing to stop.
    public static Func<CancellationToken, Task<DesktopRuntimes>> ForExternalUrl(string url) =>
        _ => Task.FromResult(new DesktopRuntimes(url, () => ValueTask.CompletedTask));

    // The real, double-click desktop experience: this process owns the whole runtime lifecycle.
    public static Func<CancellationToken, Task<DesktopRuntimes>> ForSupervisedRuntimes(IConfiguration configuration) =>
        ct => StartAsync(configuration, ct);

    private static async Task<DesktopRuntimes> StartAsync(IConfiguration configuration, CancellationToken ct)
    {
        // The credential lives in the Supervisor and is handed only to the Client Runtime. Checking
        // it here rather than before launch makes "not signed in" a visible Failed state in the
        // window instead of a silent exit before anything is on screen.
        var platform = CredentialStore.GetPlatform();
        if (platform is null || string.IsNullOrEmpty(platform.Key))
            throw new InvalidOperationException("Not signed in. Run `forge login`, then retry.");

        var (missionRuntimeUrl, mode, launcher) = await MissionRuntimeResolver.ResolveAsync(configuration, ct);
        Process? clientRuntime = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            clientRuntime = ClientRuntimeProcess.Start(
                missionRuntimeUrl, mode, platform.Key, configuration["ConversationRuntime:BaseUrl"]);
            var url = await ClientRuntimeProcess.WaitForReadyUrlAsync(clientRuntime, ct);
            return new DesktopRuntimes(url, () => StopAsync(clientRuntime, launcher));
        }
        catch
        {
            await StopAsync(clientRuntime, launcher);
            throw;
        }
    }

    private static async ValueTask StopAsync(Process? clientRuntime, IMissionRuntimeLauncher? launcher)
    {
        if (clientRuntime is not null)
        {
            await ProcessTermination.StopAsync(clientRuntime);
            clientRuntime.Dispose();
        }

        if (launcher is not null)
            await launcher.DisposeAsync();
    }
}
