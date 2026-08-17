using System.Diagnostics;

namespace ForgeMission.Desktop;

// One owner for "stop a child this Supervisor started" — the Client Runtime and the Host both go
// through here.
//
// Graceful-first, hard-kill as a fallback. Confirmed live: a hard Process.Kill() sends SIGKILL on
// Unix, giving a child no chance to run its own shutdown path. ASP.NET Core's generic host handles
// SIGTERM by draining and shutting down gracefully, which is what lets the Client Runtime's Main()
// continue past app.RunAsync(). Windows has no direct SIGTERM equivalent for another process
// without much more Win32 plumbing (matches this project's "Mac first, Windows/Linux validated
// periodically" priority) — it falls straight to the hard kill.
internal static class ProcessTermination
{
    // Docker's own default stop grace period is 10s before it SIGKILLs a container; give a graceful
    // shutdown at least that long before giving up on it.
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(10);

    // For a child with nothing to shut down gracefully. Measured live: the native host does not act
    // on SIGTERM (its native loop never returns), so asking it to stop politely only costs the full
    // grace period before the kill that was always going to happen — 10s of the app appearing to
    // linger after the user quit. The Host owns no state worth draining, so it is killed outright.
    public static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the call — nothing to do.
        }
    }

    public static async Task StopAsync(Process process)
    {
        try
        {
            if (process.HasExited)
                return;

            if (!OperatingSystem.IsWindows() && await TryTerminateGracefullyAsync(process))
                return;

            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the call — nothing to do.
        }
    }

    private static async Task<bool> TryTerminateGracefullyAsync(Process process)
    {
        using (var sigterm = Process.Start(new ProcessStartInfo("/bin/kill", ["-TERM", process.Id.ToString()])
        {
            UseShellExecute = false,
        }))
        {
            if (sigterm is not null)
                await sigterm.WaitForExitAsync();
        }

        try
        {
            await process.WaitForExitAsync().WaitAsync(GracePeriod);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
