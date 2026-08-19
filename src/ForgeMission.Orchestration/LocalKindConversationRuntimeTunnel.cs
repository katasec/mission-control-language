using System.ComponentModel;
using System.Diagnostics;

namespace ForgeMission.Orchestration;

// Owns exactly one `kubectl port-forward` process: the loopback development adapter in front of the
// Kind conversation-host service. It never creates a cluster, deploys, scales, rebuilds, reads a
// secret value, or touches a process it did not start — `make -C ../forge-infra
// 350-conversation-kind-up` remains the only thing that provisions or deploys.
internal sealed class LocalKindConversationRuntimeTunnel(Process process) : IAsyncDisposable
{
    private const string Prerequisite =
        "Could not start `kubectl port-forward` for the local Conversation Runtime. Install kubectl "
        + "and start the local runtime with `make -C ../forge-infra 350-conversation-kind-up`.";

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    public static LocalKindConversationRuntimeTunnel Start() => Start(Process.Start);

    // The spawn seam is a parameter rather than shared state, so a test supplies its own without a
    // global to reset.
    internal static LocalKindConversationRuntimeTunnel Start(Func<ProcessStartInfo, Process?> spawn)
    {
        Process? started;
        try
        {
            started = spawn(PortForwardStartInfo());
        }
        catch (Exception ex) when (ex is Win32Exception or PlatformNotSupportedException)
        {
            throw new InvalidOperationException(Prerequisite, ex);
        }

        return started is null
            ? throw new InvalidOperationException(Prerequisite)
            : new LocalKindConversationRuntimeTunnel(started);
    }

    // The single command this unit is allowed to run. Output is left inherited rather than
    // redirected: nothing here drains a pipe, and an undrained one would eventually block kubectl.
    internal static ProcessStartInfo PortForwardStartInfo()
    {
        var startInfo = new ProcessStartInfo("kubectl") { UseShellExecute = false, CreateNoWindow = true };
        startInfo.ArgumentList.Add("port-forward");
        startInfo.ArgumentList.Add("--address");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--namespace");
        startInfo.ArgumentList.Add("forge-durable");
        startInfo.ArgumentList.Add("service/conversation-host");
        startInfo.ArgumentList.Add("18080:8080");
        return startInfo;
    }

    // Stops only this process. A handle that never started, already exited, or was already disposed
    // is a no-op, so repeated disposal along the Supervisor's cleanup path is safe.
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var stop = new CancellationTokenSource(StopTimeout);
                await process.WaitForExitAsync(stop.Token);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception
            or NotSupportedException or OperationCanceledException)
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
