using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class EditToolExecutorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-edit-").FullName;
    private readonly IWorkspace _workspace;
    private readonly EditToolExecutor _tool = new();

    public EditToolExecutorTests() => _workspace = new LocalDiskWorkspace(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task UniqueMatch_Replaces()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "hello world");

        var result = await _tool.ExecuteAsync(
            Args(("file_path", "a.txt"), ("old_string", "world"), ("new_string", "there")), _workspace);

        Assert.False(result.IsError);
        Assert.Equal("hello there", File.ReadAllText(path));
    }

    [Fact]
    public async Task NonUniqueMatch_WithoutReplaceAll_FailsWithoutTouchingFile()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "foo bar foo");

        var result = await _tool.ExecuteAsync(
            Args(("file_path", "a.txt"), ("old_string", "foo"), ("new_string", "baz")), _workspace);

        Assert.True(result.IsError);
        Assert.Contains("not unique", result.Content);
        Assert.Equal("foo bar foo", File.ReadAllText(path)); // untouched
    }

    [Fact]
    public async Task NonUniqueMatch_WithReplaceAll_ReplacesEvery()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "foo bar foo");

        var result = await _tool.ExecuteAsync(
            Args(("file_path", "a.txt"), ("old_string", "foo"), ("new_string", "baz"), ("replace_all", true)), _workspace);

        Assert.False(result.IsError);
        Assert.Equal("baz bar baz", File.ReadAllText(path));
    }

    [Fact]
    public async Task OldStringNotFound_ReturnsError()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "hello world");

        var result = await _tool.ExecuteAsync(
            Args(("file_path", "a.txt"), ("old_string", "missing"), ("new_string", "x")), _workspace);

        Assert.True(result.IsError);
        Assert.Contains("not found", result.Content);
    }

    [Fact]
    public async Task NonexistentFile_ReturnsError_PointsToWrite()
    {
        var result = await _tool.ExecuteAsync(
            Args(("file_path", "nope.txt"), ("old_string", "a"), ("new_string", "b")), _workspace);

        Assert.True(result.IsError);
        Assert.Contains("Write", result.Content);
    }

    [Fact]
    public async Task PathEscapingRoot_ReturnsError_NotThrow()
    {
        var result = await _tool.ExecuteAsync(
            Args(("file_path", "../outside.txt"), ("old_string", "a"), ("new_string", "b")), _workspace);

        Assert.True(result.IsError);
        Assert.Contains("outside the workspace roots", result.Content);
    }

    private static Dictionary<string, object?> Args(params (string key, object value)[] pairs)
        => pairs.ToDictionary(p => p.key, p => (object?)p.value);
}
