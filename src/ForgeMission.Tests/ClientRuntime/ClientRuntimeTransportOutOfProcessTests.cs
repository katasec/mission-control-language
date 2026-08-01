using System.Diagnostics;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ClientRuntimeTransportOutOfProcessTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-transport-").FullName;

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    [Fact]
    public async Task Probe_ReadsRealFile_ThroughLoopbackChannel()
    {
        await File.WriteAllTextAsync(Path.Combine(_workspace, "proof.txt"), "loopback-provider-result");
        await using var host = await ClientRuntimeHostProcess.StartAsync();

        var result = await RunProbeAsync(host.BaseUrl, "proof.txt");

        Assert.True(result.ExitCode == 0, result.StandardError);
        Assert.Contains("loopback-provider-result", result.StandardOutput);
    }

    [Fact]
    public async Task Probe_ConfirmationEvent_Response_ThenRealProviderExecution()
    {
        await using var host = await ClientRuntimeHostProcess.StartAsync("RequiresUserConfirmation");

        var result = await RunProbeAsync(host.BaseUrl, "unused.txt", confirm: true);

        Assert.True(result.ExitCode == 0, result.StandardError);
        Assert.Contains("confirmation-approved", result.StandardOutput);
    }

    private async Task<ProcessResult> RunProbeAsync(string baseUrl, string fileName, bool confirm = false)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = DotnetHost(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = RepositoryRoot(),
            },
        };
        process.StartInfo.ArgumentList.Add(ProbeAssembly());
        process.StartInfo.ArgumentList.Add(baseUrl);
        process.StartInfo.ArgumentList.Add(_workspace);
        process.StartInfo.ArgumentList.Add(fileName);
        if (confirm)
            process.StartInfo.ArgumentList.Add("confirm");
        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "ForgeMission.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string HostAssembly() => Path.Combine(
        RepositoryRoot(), "src", "ForgeMission.ClientRuntime", "bin", "Debug", "net10.0", "ForgeMission.ClientRuntime.dll");

    private static string ProbeAssembly() => Path.Combine(
        RepositoryRoot(), "src", "ForgeMission.ClientRuntime.TransportProbe", "bin", "Debug", "net10.0",
        "ForgeMission.ClientRuntime.TransportProbe.dll");

    private static string DotnetHost() => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private sealed class ClientRuntimeHostProcess(Process process, string baseUrl) : IAsyncDisposable
    {
        public string BaseUrl { get; } = baseUrl;

        public static async Task<ClientRuntimeHostProcess> StartAsync(string? terminalOutcome = null)
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
            process.StartInfo.Environment["MissionRuntime__Mode"] = "none";
            if (terminalOutcome is not null)
                process.StartInfo.Environment["Authorization__TerminalOutcome"] = terminalOutcome;
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

        public ValueTask DisposeAsync()
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            process.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
