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

    public static async Task<DockerMissionRuntime> StartAsync(
        IConfiguration configuration,
        string contentRoot,
        CancellationToken ct = default)
    {
        var repositoryRoot = Path.GetFullPath(configuration["MissionRuntime:Docker:RepositoryRoot"]
            ?? Path.Combine(contentRoot, "..", ".."));
        var missionFile = Path.GetFullPath(configuration["MissionRuntime:Docker:MissionFile"]
            ?? Path.Combine(repositoryRoot, "missions", "vanilla", "mission.mcl"));
        var providerEnvironment = ProviderEnvironmentFile.Load(configuration["MissionRuntime:Docker:ProviderEnvFile"]);

        if (!File.Exists(missionFile))
            throw new InvalidOperationException($"Docker Mission Runtime mission file not found: {missionFile}");

        var relativeMissionPath = Path.GetRelativePath(repositoryRoot, missionFile);
        if (relativeMissionPath == ".." || relativeMissionPath.StartsWith($"..{Path.DirectorySeparatorChar}"))
            throw new InvalidOperationException($"Mission file must be inside Docker repository root: {repositoryRoot}");

        var docker = await DockerPrereqChecker.CheckDockerAsync();
        if (docker.Status == PrereqStatus.Fail)
            throw new InvalidOperationException($"Docker prerequisite failed: {docker.Detail}");

        if (!await DockerCli.IsImagePresentAsync(RunnerImage))
            await DockerCli.PullImageAsync(RunnerImage);

        await DockerCli.EnsureNetworkAsync("forge-net");

        var hostPort = FindFreePort();
        var containerName = $"forge-client-{Guid.NewGuid():N}"[..25];
        try
        {
            await DockerCli.RunContainerAsync(
                name: containerName,
                image: RunnerImage,
                cmd: [],
                env: [.. providerEnvironment, $"MissionFile=/workspace/{relativeMissionPath.Replace('\\', '/')}"] ,
                binds: [$"{repositoryRoot}:/workspace"],
                hostPort: hostPort,
                containerPort: RunnerPort,
                network: "forge-net");

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
