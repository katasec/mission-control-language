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

// What starting the child produced: the wait for its ready URL, and the one operation that stops it.
// Ownership is handed back before the wait, so a child that starts and then never reports ready is
// still stopped by the boot's cleanup path.
internal sealed record ApplicationHostStart(Task<string> ReadyUrl, Func<ValueTask> StopAsync);

internal delegate ApplicationHostStart StartApplicationHost(
    string missionRuntimeBaseUrl,
    string missionRuntimeMode,
    string conversationRuntimeBaseUrl,
    CancellationToken ct);

// The potentially slow work the Supervisor performs while the Host is already on screen: resolve
// where the Mission Runtime lives and prepare the durable Conversation Runtime (both Orchestration's
// decisions to carry out, never the Desktop's or the Application Host's), then start the Client
// Runtime with both verified URLs and wait for its ready URL.
//
// A boot either returns fully-started runtimes or throws having stopped whatever it partially
// started — the lifecycle never inherits a half-built runtime set.
internal static class DesktopBoot
{
    // Dev/test convenience: point at a Application Host already running elsewhere. Nothing to start,
    // so nothing to stop.
    public static Func<CancellationToken, Task<DesktopRuntimes>> ForExternalUrl(string url) =>
        _ => Task.FromResult(new DesktopRuntimes(url, () => ValueTask.CompletedTask));

    // The real, double-click desktop experience: this process owns the whole runtime lifecycle.
    public static Func<CancellationToken, Task<DesktopRuntimes>> ForSupervisedRuntimes(IConfiguration configuration) =>
        ct => StartAsync(configuration, ct);

    private static async Task<DesktopRuntimes> StartAsync(IConfiguration configuration, CancellationToken ct)
    {
        // The credential lives in the Supervisor and is handed only to the Application Host. Checking
        // it here rather than before launch makes "not signed in" a visible Failed state in the
        // window instead of a silent exit before anything is on screen.
        var platform = CredentialStore.GetPlatform();
        if (platform is null || string.IsNullOrEmpty(platform.Key))
            throw new InvalidOperationException("Not signed in. Run `forge login`, then retry.");

        return await ComposeAsync(
            token => MissionRuntimeResolver.ResolveAsync(configuration, token),
            token => ConversationRuntimeBootstrap.PrepareAsync(configuration, token),
            (missionRuntimeUrl, mode, conversationRuntimeUrl, token) =>
                StartApplicationHostProcess(missionRuntimeUrl, mode, platform.Key, conversationRuntimeUrl, token),
            ct);
    }

    // The startup order itself: both runtimes are prepared and verified before the child that
    // depends on them exists, and every failure leaves through the one cleanup path. Composed from
    // seams rather than concrete calls so that order and its cleanup contract are provable without
    // Docker, Kind, or a real child process.
    internal static async Task<DesktopRuntimes> ComposeAsync(
        Func<CancellationToken, Task<(string BaseUrl, string Mode, IMissionRuntimeLauncher? Launcher)>> prepareMissionRuntime,
        Func<CancellationToken, Task<ConversationRuntimeLease>> prepareConversationRuntime,
        StartApplicationHost startApplicationHost,
        CancellationToken ct)
    {
        var (missionRuntimeUrl, mode, launcher) = await prepareMissionRuntime(ct);
        ConversationRuntimeLease? conversation = null;
        ApplicationHostStart? applicationHost = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            conversation = await prepareConversationRuntime(ct);

            ct.ThrowIfCancellationRequested();
            applicationHost = startApplicationHost(missionRuntimeUrl, mode, conversation.BaseUrl, ct);
            var url = await applicationHost.ReadyUrl;

            return new DesktopRuntimes(url, () => StopAsync(applicationHost, conversation, launcher));
        }
        catch
        {
            await StopAsync(applicationHost, conversation, launcher);
            throw;
        }
    }

    // Starts the child and hands back its stop closure immediately; the ready wait belongs to the
    // caller, which by then already owns the termination path.
    private static ApplicationHostStart StartApplicationHostProcess(
        string missionRuntimeBaseUrl,
        string missionRuntimeMode,
        string missionRuntimeCredential,
        string conversationRuntimeBaseUrl,
        CancellationToken ct)
    {
        var process = ApplicationHostProcess.Start(
            missionRuntimeBaseUrl, missionRuntimeMode, missionRuntimeCredential, conversationRuntimeBaseUrl);

        return new ApplicationHostStart(
            ApplicationHostProcess.WaitForReadyUrlAsync(process, ct),
            () => StopApplicationHostAsync(process));
    }

    // Reverse dependency order: the child that consumes both runtimes goes first, then the tunnel
    // this Supervisor owns (if it started one), then the Mission Runtime it launched. Anything that
    // was never created is skipped, so a failure part-way through stops exactly what exists.
    private static async ValueTask StopAsync(
        ApplicationHostStart? applicationHost,
        ConversationRuntimeLease? conversation,
        IMissionRuntimeLauncher? launcher)
    {
        if (applicationHost is not null)
            await applicationHost.StopAsync();

        if (conversation is not null)
            await conversation.DisposeAsync();

        if (launcher is not null)
            await launcher.DisposeAsync();
    }

    private static async ValueTask StopApplicationHostAsync(Process process)
    {
        await ProcessTermination.StopAsync(process);
        process.Dispose();
    }
}
