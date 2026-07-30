using System.Net;
using System.Net.Sockets;
using System.Formats.Tar;
using ForgeMission.Docker;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.ClientRuntime.Services;

// Owns the local Docker Mission Runtime lifecycle. MissionRuntimeSession sees only BaseUrl, exactly
// as it does for an in-process or hosted runtime.
internal sealed class DockerMissionRuntime(string containerName, int hostPort) : IAsyncDisposable
{
    private const string RunnerImage = "ghcr.io/katasec/forge-runner:latest";
    private const int RunnerPort = 8080;
    private const string ContainerMissionParent = "/tmp";
    private const string ContainerMissionDirectory = "forge-mission";
    private const string ContainerMissionFile = "/tmp/forge-mission/mission.mcl";

    public string BaseUrl => $"http://127.0.0.1:{hostPort}/";

    public static async Task<DockerMissionRuntime> StartAsync(
        IConfiguration configuration,
        string contentRoot,
        CancellationToken ct = default)
    {
        var missionFile = Path.GetFullPath(configuration["MissionRuntime:Docker:MissionFile"]
            ?? Path.Combine(contentRoot, "..", "..", "missions", "vanilla", "mission.mcl"));
        var providerEnvironment = ProviderEnvironmentFile.Load(configuration["MissionRuntime:Docker:ProviderEnvFile"]);

        if (!File.Exists(missionFile))
            throw new InvalidOperationException($"Docker Mission Runtime mission file not found: {missionFile}");

        using var missionArchive = MissionArchive.Create(missionFile, ContainerMissionDirectory);

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
            await DockerCli.CreateContainerAsync(
                name: containerName,
                image: RunnerImage,
                cmd: [],
                env: [.. providerEnvironment, $"MissionFile={ContainerMissionFile}"],
                binds: [],
                hostPort: hostPort,
                containerPort: RunnerPort,
                network: "forge-net",
                hostIp: IPAddress.Loopback.ToString());
            await DockerCli.CopyArchiveToContainerAsync(containerName, ContainerMissionParent, missionArchive, ct);
            await DockerCli.StartContainerAsync(containerName);

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

// Builds the only filesystem input a local Docker brain receives. The archive is created by the
// Client Runtime and copied through Docker Engine; the runner never gets a host bind mount.
internal static class MissionArchive
{
    public static MemoryStream Create(string missionFile, string containerDirectory)
    {
        var missionRoot = Path.GetDirectoryName(missionFile)
            ?? throw new InvalidOperationException($"Mission file has no parent directory: {missionFile}");
        var archive = new MemoryStream();
        using (var writer = new TarWriter(archive, leaveOpen: true))
            WriteDirectory(writer, missionRoot, missionRoot, containerDirectory);

        archive.Position = 0;
        return archive;
    }

    private static void WriteDirectory(TarWriter writer, string missionRoot, string directory, string containerDirectory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Mission package cannot contain a symbolic link: {path}");

            var relativePath = Path.GetRelativePath(missionRoot, path).Replace('\\', '/');
            var archivePath = $"{containerDirectory}/{relativePath}";
            if ((attributes & FileAttributes.Directory) != 0)
            {
                writer.WriteEntry(new PaxTarEntry(TarEntryType.Directory, archivePath));
                WriteDirectory(writer, missionRoot, path, containerDirectory);
                continue;
            }

            writer.WriteEntry(path, archivePath);
        }
    }
}
