using ForgeMission.Docker;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class DockerCliTests
{
    [Fact]
    public void CreateContainerRequest_LoopbackRuntimeHasNoBinds()
    {
        var request = DockerCli.CreateContainerRequest(
            image: "ghcr.io/katasec/forge-runner:latest",
            cmd: [],
            env: ["MissionRef=ghcr.io/katasec/forge-mission-vanilla@sha256:9663e05847676da28191f09459ce45671d624221d2d9b329ff0770cb9621dc46"],
            binds: [],
            hostPort: 54321,
            containerPort: 8080,
            network: "forge-net",
            hostIp: "127.0.0.1");

        Assert.Null(request.HostConfig!.Binds);
        Assert.Contains(request.Env!, value => value.StartsWith("MissionRef=", StringComparison.Ordinal));
        Assert.DoesNotContain(request.Env!, value => value.StartsWith("MissionFile=", StringComparison.Ordinal));
        var binding = Assert.Single(request.HostConfig.PortBindings!["8080/tcp"]);
        Assert.Equal("127.0.0.1", binding.HostIp);
        Assert.Equal("54321", binding.HostPort);
    }

    [Fact]
    public void CreateContainerRequest_LeavesExistingCallersUnchangedWhenNoHostIpIsSpecified()
    {
        var request = DockerCli.CreateContainerRequest(
            image: "image",
            cmd: [],
            env: [],
            binds: ["named-volume:/data"],
            hostPort: 3000,
            containerPort: 8080,
            network: "forge-net",
            hostIp: null);

        var binds = Assert.IsType<string[]>(request.HostConfig!.Binds);
        Assert.Equal(["named-volume:/data"], binds);
        var binding = Assert.Single(request.HostConfig.PortBindings!["8080/tcp"]);
        Assert.Null(binding.HostIp);
    }
}
