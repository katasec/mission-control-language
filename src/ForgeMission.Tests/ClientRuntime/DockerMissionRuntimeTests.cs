using ForgeMission.ClientRuntime;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Tests.ClientRuntime;

// Runs only when an operator supplies a real Docker /v1 endpoint. The default test suite remains
// provider-independent; Task 2b invokes this with the ephemeral runner started by the Client Runtime.
public sealed class DockerMissionRuntimeTests
{
    [Fact]
    public void ResolveMissionRef_NoConfiguration_FallsBackToBuiltinVanilla()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Equal(BuiltinMissionReferences.Vanilla, DockerMissionRuntime.ResolveMissionRef(configuration));
    }

    [Fact]
    public void ResolveMissionRef_ExplicitConfiguration_UsesConfiguredValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("MissionRuntime:Docker:MissionRef", "ghcr.io/example/custom@sha256:abc")])
            .Build();

        Assert.Equal("ghcr.io/example/custom@sha256:abc", DockerMissionRuntime.ResolveMissionRef(configuration));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task SendAsync_RealDockerRunner_ExecutesReadAndReturnsTheHeading()
    {
        var baseUrl = Environment.GetEnvironmentVariable("FORGE_DOCKER_RUNTIME_URL");
        var workspaceRoot = Environment.GetEnvironmentVariable("FORGE_DOCKER_WORKSPACE_ROOT");
        Skip.If(string.IsNullOrWhiteSpace(baseUrl), "FORGE_DOCKER_RUNTIME_URL not set");
        Skip.If(string.IsNullOrWhiteSpace(workspaceRoot), "FORGE_DOCKER_WORKSPACE_ROOT not set");

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl!) };
        var session = new MissionRuntimeSession(http);
        var workspace = new LocalDiskWorkspace(workspaceRoot!);
        var capabilities = new CapabilityRegistry(
        [
            new WorkspaceFileProvider(workspace),
            new WorkspaceTerminalProvider(workspace),
        ]);
        var dispatcher = new CapabilityDispatcher(capabilities,
            new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default), new InMemoryCapabilityAuditLog());
        var toolLog = new List<string>();

        var answer = await session.SendAsync(
            "Read README.md and tell me its first heading.",
            capabilities,
            dispatcher,
            onToolCall: notification => toolLog.Add($"{notification.State}:{notification.Call.Name}"));

        Assert.Contains("Mission Control Language", answer);
        Assert.Contains("Running:Read", toolLog);
        Assert.Contains("Done:Read", toolLog);
    }
}
