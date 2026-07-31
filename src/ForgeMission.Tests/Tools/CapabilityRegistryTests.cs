using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class CapabilityRegistryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-capabilities-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ManifestAndToolDeclarations_MatchRegisteredProviders()
    {
        var workspace = new LocalDiskWorkspace(_root);
        var fileOnly = new CapabilityRegistry([new WorkspaceFileProvider(workspace)]);
        var allCapabilities = new CapabilityRegistry(
        [
            new WorkspaceFileProvider(workspace),
            new WorkspaceTerminalProvider(workspace),
        ]);

        Assert.Equal(["file"], fileOnly.AvailableCapabilities);
        Assert.Equal(["Edit", "Read", "Write"], fileOnly.ToolDeclarations.Select(tool => tool.Name).OrderBy(name => name));
        Assert.False(fileOnly.TryGetProvider<ITerminalProvider>(out _));

        Assert.Equal(["file", "terminal"], allCapabilities.AvailableCapabilities);
        Assert.Equal(["Bash", "Edit", "Read", "Write"], allCapabilities.ToolDeclarations.Select(tool => tool.Name).OrderBy(name => name));
        Assert.True(allCapabilities.TryGetProvider<IFileProvider>(out var files));
        Assert.IsType<WorkspaceFileProvider>(files);
    }
}
