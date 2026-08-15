using ForgeMission.ClientRuntime.Services;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class DefaultWorkspaceTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("forge-default-workspace-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void CreateNext_CreatesFirstNumericWorkspace()
    {
        var defaultRoot = Path.Combine(root, "source", "repos");
        var workspace = DefaultWorkspace.CreateNext(defaultRoot);

        Assert.Equal(Path.Combine(defaultRoot, "0001"), workspace);
        Assert.True(Directory.Exists(workspace));
    }

    [Fact]
    public void CreateNext_UsesNumberAfterHighestExistingNumericWorkspace()
    {
        Directory.CreateDirectory(Path.Combine(root, "0001"));
        Directory.CreateDirectory(Path.Combine(root, "0003"));
        Directory.CreateDirectory(Path.Combine(root, "notes"));

        var workspace = DefaultWorkspace.CreateNext(root);

        Assert.Equal(Path.Combine(root, "0004"), workspace);
    }
}
