using System.Security.Cryptography;
using System.Text;
using ForgeMission.Api;
using ForgeMission.Billing;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Rooms.Tests;

public sealed class FileArtifactStoreTests
{
    [Fact]
    public async Task Save_and_open_round_trip_bytes_and_metadata_for_owner()
    {
        var root = NewRoot();
        var store = NewStore(root);
        var owner = new PlatformKeyContext(Guid.NewGuid(), BalanceMicroUsd: 1);
        var bytes = Encoding.UTF8.GetBytes("artifact bytes");
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var saved = await store.SaveAsync(
            new ArtifactWriteRequest("../scan.jpg", "image/jpeg", sha, ArtifactRole.Input, bytes.Length),
            new MemoryStream(bytes),
            owner,
            CancellationToken.None);

        Assert.True(saved.Sha256Matched);
        Assert.Equal("scan.jpg", saved.Artifact.Name);
        Assert.Equal("image/jpeg", saved.Artifact.ContentType);
        Assert.Equal(bytes.Length, saved.Artifact.Size);
        Assert.Equal(sha, saved.Artifact.Sha256);

        await using var read = await store.OpenAsync(saved.Artifact.Id, owner, CancellationToken.None);
        Assert.NotNull(read);
        using var copy = new MemoryStream();
        await read.Content.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Open_returns_null_for_wrong_owner()
    {
        var root = NewRoot();
        var store = NewStore(root);
        var owner = new PlatformKeyContext(Guid.NewGuid(), BalanceMicroUsd: 1);
        var otherOwner = new PlatformKeyContext(Guid.NewGuid(), BalanceMicroUsd: 1);

        var saved = await store.SaveAsync(
            new ArtifactWriteRequest("scan.jpg", "image/jpeg", "", ArtifactRole.Input, DeclaredSize: 1),
            new MemoryStream([1]),
            owner,
            CancellationToken.None);

        var read = await store.OpenAsync(saved.Artifact.Id, otherOwner, CancellationToken.None);

        Assert.Null(read);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Delete_removes_artifact_for_owner_and_is_idempotent()
    {
        var root = NewRoot();
        var store = NewStore(root);
        var owner = new PlatformKeyContext(Guid.NewGuid(), BalanceMicroUsd: 1);

        var saved = await store.SaveAsync(
            new ArtifactWriteRequest("scan.jpg", "image/jpeg", "", ArtifactRole.Input, DeclaredSize: 1),
            new MemoryStream([1]),
            owner,
            CancellationToken.None);

        await store.DeleteAsync(saved.Artifact.Id, owner, CancellationToken.None);
        await store.DeleteAsync(saved.Artifact.Id, owner, CancellationToken.None);

        var read = await store.OpenAsync(saved.Artifact.Id, owner, CancellationToken.None);
        Assert.Null(read);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Delete_wrong_owner_does_not_remove_artifact()
    {
        var root = NewRoot();
        var store = NewStore(root);
        var owner = new PlatformKeyContext(Guid.NewGuid(), BalanceMicroUsd: 1);
        var otherOwner = new PlatformKeyContext(Guid.NewGuid(), BalanceMicroUsd: 1);

        var saved = await store.SaveAsync(
            new ArtifactWriteRequest("scan.jpg", "image/jpeg", "", ArtifactRole.Input, DeclaredSize: 1),
            new MemoryStream([1]),
            owner,
            CancellationToken.None);

        await store.DeleteAsync(saved.Artifact.Id, otherOwner, CancellationToken.None);

        await using var read = await store.OpenAsync(saved.Artifact.Id, owner, CancellationToken.None);
        Assert.NotNull(read);
        Directory.Delete(root, recursive: true);
    }


    [Fact]
    public async Task Save_rejects_declared_size_above_limit()
    {
        var store = NewStore(NewRoot());
        var owner = new PlatformKeyContext(Guid.NewGuid(), BalanceMicroUsd: 1);

        await Assert.ThrowsAsync<ArtifactTooLargeException>(() =>
            store.SaveAsync(
                new ArtifactWriteRequest(
                    "huge.bin",
                    "application/octet-stream",
                    "",
                    ArtifactRole.Input,
                    FileArtifactStore.MaxBytes + 1),
                Stream.Null,
                owner,
                CancellationToken.None));
    }

    private static FileArtifactStore NewStore(string root)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Artifacts:LocalRoot"] = root,
            })
            .Build();
        return new FileArtifactStore(configuration);
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "forge-artifact-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
