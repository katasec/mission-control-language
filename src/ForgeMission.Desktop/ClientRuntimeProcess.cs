using System.Diagnostics;
using System.Text;

namespace ForgeMission.Desktop;

// The Client Runtime child: started by the Supervisor, handed its already-resolved Mission Runtime
// URL and the platform credential, and stopped by the Supervisor's single cleanup path. The Host
// never sees any of this.
internal static class ClientRuntimeProcess
{
    private const string ReadyUrlPrefix = "FORGE_CLIENT_RUNTIME_URL=";
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(20);

    public static Process Start(
        string missionRuntimeBaseUrl,
        string missionRuntimeMode,
        string missionRuntimeCredential,
        string conversationRuntimeBaseUrl)
    {
        var process = new Process
        {
            StartInfo = BuildStartInfo(
                missionRuntimeBaseUrl, missionRuntimeMode, missionRuntimeCredential, conversationRuntimeBaseUrl),
            EnableRaisingEvents = true,
        };

        process.Start();
        return process;
    }

    // Everything the child is told, in one place it can be asserted from: both runtime URLs are
    // already resolved and verified by the time the Supervisor gets here, so the durable one is
    // passed unconditionally — a Janus send must never fall back to a relative URI with no base
    // address.
    internal static ProcessStartInfo BuildStartInfo(
        string missionRuntimeBaseUrl,
        string missionRuntimeMode,
        string missionRuntimeCredential,
        string conversationRuntimeBaseUrl)
    {
        var (fileName, dllArgument) = SiblingExecutable.Resolve("ForgeMission.ClientRuntime");
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (dllArgument is not null)
            startInfo.ArgumentList.Add(dllArgument);

        startInfo.EnvironmentVariables["MissionRuntime__BaseUrl"] = missionRuntimeBaseUrl;
        startInfo.EnvironmentVariables["MissionRuntime__Mode"] = missionRuntimeMode;
        startInfo.EnvironmentVariables["MissionRuntime__Credential"] = missionRuntimeCredential;
        startInfo.EnvironmentVariables["ConversationRuntime__BaseUrl"] = conversationRuntimeBaseUrl;
        return startInfo;
    }

    // Genuinely asynchronous: the Supervisor no longer runs a native loop, so a slow Client Runtime
    // start blocks nothing — the Host is already up and showing Booting while this waits.
    public static async Task<string> WaitForReadyUrlAsync(Process process, CancellationToken ct)
    {
        var readyUrl = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data?.StartsWith(ReadyUrlPrefix, StringComparison.Ordinal) == true)
                readyUrl.TrySetResult(e.Data[ReadyUrlPrefix.Length..]);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            return await readyUrl.Task.WaitAsync(ReadyTimeout, ct);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException($"Client Runtime did not start within {ReadyTimeout.TotalSeconds:0}s. {stderr}");
        }
    }
}
