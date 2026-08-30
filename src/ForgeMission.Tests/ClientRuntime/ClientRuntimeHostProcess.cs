using System.Diagnostics;

namespace ForgeMission.Tests.ClientRuntime;

/// <summary>
/// Starts the real ForgeMission.ClientRuntime process and reports the URL it printed. One owner for
/// process start-up, shared by the out-of-process transport probe tests and the Project transport
/// contract tests.
/// </summary>
/// <remarks>
/// <paramref name="profileRoot"/> redirects the child's user-profile lookup (HOME on Unix,
/// USERPROFILE on Windows), which is what keeps a test's Projects under
/// <c>&lt;profileRoot&gt;/Forge/Projects</c> instead of the developer's real home directory. The
/// redirect is a property of the child process, so shipping code needs no configuration knob for it.
/// </remarks>
internal sealed class ClientRuntimeHostProcess(Process process, string baseUrl) : IAsyncDisposable
{
    public string BaseUrl { get; } = baseUrl;

    public static async Task<ClientRuntimeHostProcess> StartAsync(
        string? terminalOutcome = null, string? profileRoot = null)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(DotnetHost(), HostAssembly())
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = RepositoryRoot(),
            },
        };
        process.StartInfo.Environment["MissionRuntime__BaseUrl"] = "http://127.0.0.1:8080/";
        process.StartInfo.Environment["MissionRuntime__Credential"] = "local";
        if (terminalOutcome is not null)
            process.StartInfo.Environment["Authorization__TerminalOutcome"] = terminalOutcome;
        if (profileRoot is not null)
        {
            process.StartInfo.Environment["HOME"] = profileRoot;
            process.StartInfo.Environment["USERPROFILE"] = profileRoot;
        }

        process.Start();

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            if (line?.StartsWith("FORGE_CLIENT_RUNTIME_URL=", StringComparison.Ordinal) == true)
                return new ClientRuntimeHostProcess(process, line["FORGE_CLIENT_RUNTIME_URL=".Length..]);
        }

        var error = await process.StandardError.ReadToEndAsync();
        process.Kill(entireProcessTree: true);
        throw new InvalidOperationException($"Client Runtime did not start. {error}");
    }

    public static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "ForgeMission.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    public static string DotnetHost() => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private static string HostAssembly() => Path.Combine(
        RepositoryRoot(), "src", "ForgeMission.ClientRuntime", "bin", "Debug", "net10.0", "ForgeMission.ClientRuntime.dll");

    public ValueTask DisposeAsync()
    {
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
        process.Dispose();
        return ValueTask.CompletedTask;
    }
}
