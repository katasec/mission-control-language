using ForgeMission.ClientRuntime.Services;
using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class WorkspaceStateTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("forge-client-runtime-").FullName;

    [Fact]
    public void OpenFolder_ConstructsLocalDiskWorkspaceAtSelectedRoot()
    {
        var state = new WorkspaceState();

        state.OpenFolder(root);

        var workspace = Assert.IsType<LocalDiskWorkspace>(state.Workspace);
        Assert.Equal(root, workspace.Roots[0]);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
