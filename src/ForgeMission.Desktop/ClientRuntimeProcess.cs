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
        string? conversationRuntimeBaseUrl)
    {
        var (fileName, dllArgument) = SiblingExecutable.Resolve("ForgeMission.ClientRuntime");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };
        if (dllArgument is not null)
            process.StartInfo.ArgumentList.Add(dllArgument);

        process.StartInfo.EnvironmentVariables["MissionRuntime__BaseUrl"] = missionRuntimeBaseUrl;
        process.StartInfo.EnvironmentVariables["MissionRuntime__Mode"] = missionRuntimeMode;
        process.StartInfo.EnvironmentVariables["MissionRuntime__Credential"] = missionRuntimeCredential;
        // Only forwarded when configured — a normal (non-Janus) run must not require it.
        if (!string.IsNullOrWhiteSpace(conversationRuntimeBaseUrl))
            process.StartInfo.EnvironmentVariables["ConversationRuntime__BaseUrl"] = conversationRuntimeBaseUrl;

        process.Start();
        return process;
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
