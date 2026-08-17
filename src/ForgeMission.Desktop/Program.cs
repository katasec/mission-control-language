using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Desktop;

// The Desktop Supervisor: the process the user launches. It owns Mission Runtime resolution, the
// Client Runtime child, the native Host child, and every cleanup path — and it deliberately owns no
// window. The native host is a separate, disposable process (ForgeMission.Desktop.Host); this file
// never names a concrete host or the host contract. See
// docs/design/forge-architecture.md#desktop-supervisor-and-native-host-are-separate-processes.
//
// Two ways to run: pass a Client Runtime URL explicitly (dev/test convenience — points at a Client
// Runtime already running elsewhere), or pass nothing and this process owns the whole runtime
// lifecycle (the real, double-click desktop experience — publish every project into one folder and
// run only this one).
//
// A named entry class rather than top-level statements: this assembly is referenced by the test
// project, and a generated global `Program` type collides with names there.
internal static class DesktopSupervisor
{
    private static async Task<int> Main(string[] args)
    {
        if (ResolveBoot(args) is not { } boot)
        {
            Console.Error.WriteLine("Usage: ForgeMission.Desktop [<client-runtime-url>]");
            return 1;
        }

        using var stopRequested = new CancellationTokenSource();

        // AppDomain.ProcessExit does NOT reliably fire on an external `kill`/SIGTERM (confirmed by
        // testing: the child was still orphaned after `kill -TERM` even with a ProcessExit handler
        // registered) — PosixSignalRegistration is the mechanism that actually intercepts the signal
        // before the process dies. Cancelling the default termination lets the lifecycle's own
        // cleanup run to completion instead of racing it. Nothing can catch SIGKILL; that is a
        // universal OS-level limitation, not specific to this process.
        void RequestStop(PosixSignalContext context)
        {
            context.Cancel = true;
            stopRequested.Cancel();
        }

        using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, RequestStop);
        using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, RequestStop);

        await using var host = new HostProcess();
        await new DesktopLifecycle(host, boot).RunAsync(stopRequested.Token);
        return 0;
    }

    private static Func<CancellationToken, Task<DesktopRuntimes>>? ResolveBoot(string[] args)
    {
        if (args.Length == 0)
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            return DesktopBoot.ForSupervisedRuntimes(configuration);
        }

        return args.Length == 1 &&
               Uri.TryCreate(args[0], UriKind.Absolute, out var explicitUrl) &&
               explicitUrl.Scheme is "http" or "https"
            ? DesktopBoot.ForExternalUrl(explicitUrl.AbsoluteUri)
            : null;
    }
}
