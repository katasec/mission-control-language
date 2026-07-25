using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class WriteToolExecutorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-write-").FullName;
    private readonly IWorkspace _workspace;
    private readonly WriteToolExecutor _tool = new();

    public WriteToolExecutorTests() => _workspace = new LocalDiskWorkspace(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task CreatesNewFile()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "new.txt"), ("content", "hello")), _workspace);

        Assert.False(result.IsError);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_root, "new.txt")));
    }

    [Fact]
    public async Task OverwritesExistingFile()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "old");

        var result = await _tool.ExecuteAsync(Args(("file_path", "a.txt"), ("content", "new")), _workspace);

        Assert.False(result.IsError);
        Assert.Equal("new", File.ReadAllText(path));
    }

    [Fact]
    public async Task PathEscapingRoot_ReturnsError_NotThrow()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "../escape.txt"), ("content", "x")), _workspace);

        Assert.True(result.IsError);
        Assert.Contains("outside the workspace roots", result.Content);
        Assert.False(File.Exists(Path.Combine(Directory.GetParent(_root)!.FullName, "escape.txt")));
    }

    [Fact]
    public async Task MissingContent_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "a.txt")), _workspace);
        Assert.True(result.IsError);
    }

    private static Dictionary<string, object?> Args(params (string key, object value)[] pairs)
        => pairs.ToDictionary(p => p.key, p => (object?)p.value);
}
