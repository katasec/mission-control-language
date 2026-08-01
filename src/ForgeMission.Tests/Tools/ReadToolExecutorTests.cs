using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class ReadToolExecutorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-read-").FullName;
    private readonly CapabilityRegistry _capabilities;
    private readonly ICapabilityDispatcher _dispatcher;
    private readonly ReadToolExecutor _tool = new();

    public ReadToolExecutorTests()
    {
        var workspace = new LocalDiskWorkspace(_root);
        _capabilities = new CapabilityRegistry([new WorkspaceFileProvider(workspace)]);
        _dispatcher = DefaultDispatcher(_capabilities);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task ReadsFullFileContent()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "line1\nline2\nline3");

        var result = await _tool.ExecuteAsync(Args(("file_path", "a.txt")), _dispatcher);

        Assert.False(result.IsError);
        Assert.Equal("line1\nline2\nline3", result.Content);
    }

    [Fact]
    public async Task Offset_And_Limit_SliceLines()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "l1\nl2\nl3\nl4\nl5");

        var result = await _tool.ExecuteAsync(Args(("file_path", "a.txt"), ("offset", 1), ("limit", 2)), _dispatcher);

        Assert.Equal("l2\nl3", result.Content);
    }

    [Fact]
    public async Task MissingFile_ReturnsError_NotThrow()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "nope.txt")), _dispatcher);

        Assert.True(result.IsError);
        Assert.Contains("not found", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PathEscapingRoot_ReturnsError_NotThrow()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "../outside.txt")), _dispatcher);

        Assert.True(result.IsError);
        Assert.Contains("outside the workspace roots", result.Content);
    }

    [Fact]
    public async Task MissingFilePath_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(Args(), _dispatcher);
        Assert.True(result.IsError);
    }

    private static Dictionary<string, object?> Args(params (string key, object value)[] pairs)
        => pairs.ToDictionary(p => p.key, p => (object?)p.value);

    private static ICapabilityDispatcher DefaultDispatcher(CapabilityRegistry capabilities) =>
        new CapabilityDispatcher(capabilities, new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            new InMemoryCapabilityAuditLog());
}
