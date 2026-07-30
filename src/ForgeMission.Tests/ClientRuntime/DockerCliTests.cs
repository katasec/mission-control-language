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
            env: ["MissionFile=/tmp/forge-mission/mission.mcl"],
            binds: [],
            hostPort: 54321,
            containerPort: 8080,
            network: "forge-net",
            hostIp: "127.0.0.1");

        Assert.Null(request.HostConfig!.Binds);
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
