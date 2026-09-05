using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ProjectWorkbenchServiceTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("forge-workbench-").FullName;

    public void Dispose() => Directory.Delete(_profile, recursive: true);

    [Fact]
    public async Task SelectionAndRootLockDocument_AreSessionIndependentRuntimeOperations()
    {
        var store = new ProjectStore(Path.Combine(_profile, "Forge", "Projects"));
        var project = store.Create("Build a bounded Explorer.", null, null);
        await File.WriteAllTextAsync(Path.Combine(project.Home, "mcl.lock"), "lock-version = 1");
        var service = new ProjectWorkbenchService(store);

        var selection = await service.SelectMissionAsync(project.Home, "Naive", CancellationToken.None);
        var projection = service.GetProjection(project.Home);
        var document = service.OpenDocument(project.Home, "lock");

        Assert.Null(selection.Error);
        Assert.Equal("Naive", selection.Missions!.Selected);
        Assert.Contains(projection.Projection!.Assets, entry => entry.Id == "lock");
        Assert.Equal("lock-version = 1", document.Document!.Content);
    }

    [Fact]
    public void BinaryAndUnknownDocuments_AreRefusedWithoutReturningPartialContent()
    {
        var store = new ProjectStore(Path.Combine(_profile, "Forge", "Projects"));
        var project = store.Create("Keep document reads safe.", null, null);
        File.WriteAllBytes(Path.Combine(project.Home, "mcl.lock"), [0, 1, 2]);
        var service = new ProjectWorkbenchService(store);

        var binary = service.OpenDocument(project.Home, "lock");
        var unknown = service.OpenDocument(project.Home, "../../outside");

        Assert.Null(binary.Document);
        Assert.Equal(ProjectOperationErrorCode.DocumentBinary, binary.Error!.Code);
        Assert.Null(unknown.Document);
        Assert.Equal(ProjectOperationErrorCode.DocumentUnavailable, unknown.Error!.Code);
    }

    [Fact]
    public void DocumentHashes_AcceptOnlyTheDocumentedSha256Prefix()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("immutable content");
        var hex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

        Assert.True(ProjectWorkbenchService.MatchesHash(bytes, $"sha256:{hex}"));
        Assert.False(ProjectWorkbenchService.MatchesHash(bytes, hex));
        Assert.False(ProjectWorkbenchService.MatchesHash(bytes, "md5:" + hex));
    }
}
