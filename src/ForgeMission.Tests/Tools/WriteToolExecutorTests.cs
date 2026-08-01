using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class WriteToolExecutorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-write-").FullName;
    private readonly CapabilityRegistry _capabilities;
    private readonly ICapabilityDispatcher _dispatcher;
    private readonly WriteToolExecutor _tool = new();

    public WriteToolExecutorTests()
    {
        var workspace = new LocalDiskWorkspace(_root);
        _capabilities = new CapabilityRegistry([new WorkspaceFileProvider(workspace)]);
        _dispatcher = DefaultDispatcher(_capabilities);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task CreatesNewFile()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "new.txt"), ("content", "hello")), _dispatcher);

        Assert.False(result.IsError);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_root, "new.txt")));
    }

    [Fact]
    public async Task OverwritesExistingFile()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "old");

        var result = await _tool.ExecuteAsync(Args(("file_path", "a.txt"), ("content", "new")), _dispatcher);

        Assert.False(result.IsError);
        Assert.Equal("new", File.ReadAllText(path));
    }

    [Fact]
    public async Task PathEscapingRoot_ReturnsError_NotThrow()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "../escape.txt"), ("content", "x")), _dispatcher);

        Assert.True(result.IsError);
        Assert.Contains("outside the workspace roots", result.Content);
        Assert.False(File.Exists(Path.Combine(Directory.GetParent(_root)!.FullName, "escape.txt")));
    }

    [Fact]
    public async Task MissingContent_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "a.txt")), _dispatcher);
        Assert.True(result.IsError);
    }

    private static Dictionary<string, object?> Args(params (string key, object value)[] pairs)
        => pairs.ToDictionary(p => p.key, p => (object?)p.value);

    private static ICapabilityDispatcher DefaultDispatcher(CapabilityRegistry capabilities) =>
        new CapabilityDispatcher(capabilities, new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            new InMemoryCapabilityAuditLog());
}
