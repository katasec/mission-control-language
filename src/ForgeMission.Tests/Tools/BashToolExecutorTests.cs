using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class BashToolExecutorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-bash-").FullName;
    private readonly CapabilityRegistry _capabilities;
    private readonly ICapabilityDispatcher _dispatcher;

    public BashToolExecutorTests()
    {
        var workspace = new LocalDiskWorkspace(_root);
        _capabilities = new CapabilityRegistry([new WorkspaceTerminalProvider(workspace)]);
        _dispatcher = DefaultDispatcher(_capabilities);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task RunsCommand_ReturnsStdout()
    {
        var tool   = new BashToolExecutor();
        var result = await tool.ExecuteAsync(Args(("command", "echo hello")), _dispatcher);

        Assert.False(result.IsError);
        Assert.Contains("hello", result.Content);
    }

    [Fact]
    public async Task NonZeroExit_ReturnsError_NotThrow()
    {
        var tool   = new BashToolExecutor();
        var exit3  = OperatingSystem.IsWindows() ? "exit 3" : "exit 3";
        var result = await tool.ExecuteAsync(Args(("command", exit3)), _dispatcher);

        Assert.True(result.IsError);
        Assert.Contains("exited with code 3", result.Content);
    }

    [Fact]
    public async Task RunsInsideWorkspaceRoot()
    {
        File.WriteAllText(Path.Combine(_root, "marker.txt"), "x");
        var tool = new BashToolExecutor();
        var command = OperatingSystem.IsWindows() ? "dir /b" : "ls";

        var result = await tool.ExecuteAsync(Args(("command", command)), _dispatcher);

        Assert.Contains("marker.txt", result.Content);
    }

    [Fact]
    public async Task MissingCommand_ReturnsError()
    {
        var tool   = new BashToolExecutor();
        var result = await tool.ExecuteAsync(Args(), _dispatcher);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task LongRunningCommand_TimesOut()
    {
        var workspace = new LocalDiskWorkspace(_root, timeout: TimeSpan.FromMilliseconds(300));
        var capabilities = new CapabilityRegistry([new WorkspaceTerminalProvider(workspace)]);
        var dispatcher = DefaultDispatcher(capabilities);
        var tool    = new BashToolExecutor();
        var sleep6s = OperatingSystem.IsWindows() ? "ping -n 7 127.0.0.1" : "sleep 6";

        var result = await tool.ExecuteAsync(Args(("command", sleep6s)), dispatcher);

        Assert.True(result.IsError);
        Assert.Contains("timed out", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> Args(params (string key, object value)[] pairs)
        => pairs.ToDictionary(p => p.key, p => (object?)p.value);

    private static ICapabilityDispatcher DefaultDispatcher(CapabilityRegistry capabilities) =>
        new CapabilityDispatcher(capabilities, new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            new InMemoryCapabilityAuditLog());
}
