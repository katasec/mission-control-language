using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class LocalDiskWorkspaceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-workspace-").FullName;
    private readonly string _secondRoot = Directory.CreateTempSubdirectory("forge-workspace-second-").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        Directory.Delete(_secondRoot, recursive: true);
    }

    [Fact]
    public void TryResolvePath_AcceptsAnyConfiguredRoot()
    {
        var workspace = new LocalDiskWorkspace([_root, _secondRoot]);
        var secondRootFile = Path.Combine(_secondRoot, "file.txt");

        Assert.True(workspace.TryResolvePath("local.txt", out var primaryResolved, out var primaryError));
        Assert.Null(primaryError);
        Assert.StartsWith(_root, primaryResolved);

        Assert.True(workspace.TryResolvePath(secondRootFile, out var secondResolved, out var secondError));
        Assert.Null(secondError);
        Assert.Equal(secondRootFile, secondResolved);
    }

    [Fact]
    public void TryResolvePath_RejectsPathOutsideEveryRoot()
    {
        var workspace = new LocalDiskWorkspace([_root, _secondRoot]);
        var outsideRoot = Directory.CreateTempSubdirectory("forge-workspace-outside-").FullName;
        try
        {
            var outsideFile = Path.Combine(outsideRoot, "file.txt");

            Assert.False(workspace.TryResolvePath(outsideFile, out _, out var error));
            Assert.Contains("outside the workspace roots", error);
        }
        finally
        {
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WorkingDirOutsideRoots_ReturnsError()
    {
        var workspace = new LocalDiskWorkspace(_root);
        var outsideRoot = Directory.CreateTempSubdirectory("forge-workspace-outside-").FullName;
        try
        {
            var result = await workspace.ExecuteAsync("echo hello", workingDir: outsideRoot);

            Assert.True(result.IsError);
            Assert.Contains("outside the workspace roots", result.Content);
        }
        finally
        {
            Directory.Delete(outsideRoot, recursive: true);
        }
    }
}
