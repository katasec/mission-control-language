using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Tools;

public sealed class ToolExecutorRegistryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-registry-").FullName;
    private readonly CapabilityRegistry _capabilities;
    private readonly ICapabilityDispatcher _dispatcher;

    public ToolExecutorRegistryTests()
    {
        var workspace = new LocalDiskWorkspace(_root);
        _capabilities = new CapabilityRegistry(
        [
            new WorkspaceFileProvider(workspace),
            new WorkspaceTerminalProvider(workspace),
        ]);
        _dispatcher = new CapabilityDispatcher(_capabilities,
            new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default), new InMemoryCapabilityAuditLog());
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Default_CoversExactlyTheEssentials()
    {
        var names = ToolExecutorRegistry.Default().Select(e => e.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Bash", "Edit", "Read", "Write"], names);
    }

    [Fact]
    public async Task DispatchesByName_ToTheMatchingExecutor()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "content");
        var registry = new ToolExecutorRegistry();

        var call = new FunctionCallContent("call1", "Read",
            new Dictionary<string, object?> { ["file_path"] = "a.txt" });

        var result = await registry.ExecuteAsync(call, _dispatcher);

        Assert.False(result.IsError);
        Assert.Equal("content", result.Content);
    }

    [Fact]
    public async Task UnknownToolName_ReturnsGracefulError_NotThrow()
    {
        var registry = new ToolExecutorRegistry();
        var call = new FunctionCallContent("call1", "WebFetch", new Dictionary<string, object?>());

        var result = await registry.ExecuteAsync(call, _dispatcher);

        Assert.True(result.IsError);
        Assert.Contains("Unknown tool: WebFetch", result.Content);
    }
}
