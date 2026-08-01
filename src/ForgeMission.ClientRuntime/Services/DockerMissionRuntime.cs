using System.Net;
using System.Net.Sockets;
using ForgeMission.Docker;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.ClientRuntime.Services;

// Owns the local Docker Mission Runtime lifecycle. MissionRuntimeSession sees only BaseUrl, exactly
// as it does for an in-process or hosted runtime.
internal sealed class DockerMissionRuntime(string containerName, int hostPort) : IAsyncDisposable
{
    private const string RunnerImage = "ghcr.io/katasec/forge-runner:latest";
    private const int RunnerPort = 8080;
    public string BaseUrl => $"http://127.0.0.1:{hostPort}/";

    // Falls back to the built-in vanilla mission so the desktop app works with zero configuration
    // — split out from StartAsync so it's testable without needing a real Docker daemon.
    internal static string ResolveMissionRef(IConfiguration configuration)
    {
        var configured = configuration["MissionRuntime:Docker:MissionRef"];
        return string.IsNullOrWhiteSpace(configured) ? BuiltinMissionReferences.Vanilla : configured;
    }

    public static async Task<DockerMissionRuntime> StartAsync(
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var missionRef = ResolveMissionRef(configuration);
        var runnerImage = configuration["MissionRuntime:Docker:Image"] ?? RunnerImage;

        var providerEnvironment = ProviderEnvironmentFile.Load(configuration["MissionRuntime:Docker:ProviderEnvFile"]);

        var docker = await DockerPrereqChecker.CheckDockerAsync();
        if (docker.Status == PrereqStatus.Fail)
            throw new InvalidOperationException($"Docker prerequisite failed: {docker.Detail}");

        if (!await DockerCli.IsImagePresentAsync(runnerImage))
            await DockerCli.PullImageAsync(runnerImage);

        await DockerCli.EnsureNetworkAsync("forge-net");

        var hostPort = FindFreePort();
        var containerName = $"forge-client-{Guid.NewGuid():N}"[..25];
        try
        {
            await DockerCli.RunContainerAsync(
                name: containerName,
                image: runnerImage,
                cmd: [],
                env: [.. providerEnvironment, $"MissionRef={missionRef}"],
                binds: [],
                hostPort: hostPort,
                containerPort: RunnerPort,
                network: "forge-net",
                hostIp: IPAddress.Loopback.ToString());

            var runtime = new DockerMissionRuntime(containerName, hostPort);
            if (!await runtime.WaitUntilHealthyAsync(ct))
                throw new InvalidOperationException($"Docker Mission Runtime did not become healthy at {runtime.BaseUrl}health.");

            return runtime;
        }
        catch
        {
            await DockerCli.StopAndRemoveAsync(containerName);
            throw;
        }
    }

    public async ValueTask DisposeAsync() => await DockerCli.StopAndRemoveAsync(containerName);

    private async Task<bool> WaitUntilHealthyAsync(CancellationToken ct)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync($"{BaseUrl}health", ct);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (HttpRequestException) { }

            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }

        return false;
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
