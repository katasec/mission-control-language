using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class ReadToolExecutorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-read-").FullName;
    private readonly IWorkspace _workspace;
    private readonly ReadToolExecutor _tool = new();

    public ReadToolExecutorTests() => _workspace = new LocalDiskWorkspace(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task ReadsFullFileContent()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "line1\nline2\nline3");

        var result = await _tool.ExecuteAsync(Args(("file_path", "a.txt")), _workspace);

        Assert.False(result.IsError);
        Assert.Equal("line1\nline2\nline3", result.Content);
    }

    [Fact]
    public async Task Offset_And_Limit_SliceLines()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "l1\nl2\nl3\nl4\nl5");

        var result = await _tool.ExecuteAsync(Args(("file_path", "a.txt"), ("offset", 1), ("limit", 2)), _workspace);

        Assert.Equal("l2\nl3", result.Content);
    }

    [Fact]
    public async Task MissingFile_ReturnsError_NotThrow()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "nope.txt")), _workspace);

        Assert.True(result.IsError);
        Assert.Contains("not found", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PathEscapingRoot_ReturnsError_NotThrow()
    {
        var result = await _tool.ExecuteAsync(Args(("file_path", "../outside.txt")), _workspace);

        Assert.True(result.IsError);
        Assert.Contains("outside the workspace roots", result.Content);
    }

    [Fact]
    public async Task MissingFilePath_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(Args(), _workspace);
        Assert.True(result.IsError);
    }

    private static Dictionary<string, object?> Args(params (string key, object value)[] pairs)
        => pairs.ToDictionary(p => p.key, p => (object?)p.value);
}
