using System.Collections.Concurrent;
using System.Security.Cryptography;
using ForgeMission.Billing;

namespace ForgeMission.Api;

/// <summary>
/// Ephemeral artifact scratch for API A. This is run I/O, not document storage: callers upload
/// bytes, a mission consumes them, and produced bytes are downloaded by id. The store authorizes by
/// artifact id + platform-key principal; unknown and wrong-owner reads both return null.
/// </summary>
public interface IArtifactStore
{
    Task<ArtifactSaveResult> SaveAsync(
        ArtifactWriteRequest request,
        Stream content,
        PlatformKeyContext owner,
        CancellationToken ct);

    Task<ArtifactRead?> OpenAsync(string artifactId, PlatformKeyContext owner, CancellationToken ct);
}

public sealed record ArtifactWriteRequest(
    string Name,
    string ContentType,
    string Sha256,
    string Role,
    long? DeclaredSize);

public sealed record ArtifactSaveResult(MissionArtifact Artifact, bool Sha256Matched);

public sealed record ArtifactRead(MissionArtifact Artifact, Stream Content) : IAsyncDisposable
{
    public async ValueTask DisposeAsync() => await Content.DisposeAsync();
}

public sealed class FileArtifactStore(IConfiguration configuration) : IArtifactStore
{
    public const long MaxBytes = 100L * 1024L * 1024L;

    private readonly string _root = configuration["Artifacts:LocalRoot"]
        ?? Path.Combine(Path.GetTempPath(), "forge-api-artifacts");

    private readonly ConcurrentDictionary<string, StoredArtifact> _artifacts = new();

    public async Task<ArtifactSaveResult> SaveAsync(
        ArtifactWriteRequest request,
        Stream content,
        PlatformKeyContext owner,
        CancellationToken ct)
    {
        if (request.DeclaredSize is > MaxBytes)
            throw new ArtifactTooLargeException(MaxBytes);

        Directory.CreateDirectory(_root);

        var id = "art_" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(_root, id);
        var (size, sha256) = await WriteAndHashAsync(path, content, ct);
        if (size > MaxBytes)
        {
            File.Delete(path);
            throw new ArtifactTooLargeException(MaxBytes);
        }

        var artifact = new MissionArtifact
        {
            Id = id,
            Name = SafeName(request.Name),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType,
            Size = size,
            Sha256 = sha256,
            Role = request.Role,
        };

        _artifacts[id] = new StoredArtifact(owner.MemberId, artifact, path);
        return new ArtifactSaveResult(artifact, Sha256Matched: ShaMatches(request.Sha256, sha256));
    }

    public Task<ArtifactRead?> OpenAsync(string artifactId, PlatformKeyContext owner, CancellationToken ct)
    {
        if (!_artifacts.TryGetValue(artifactId, out var stored)
            || stored.Owner != owner.MemberId
            || !File.Exists(stored.Path))
            return Task.FromResult<ArtifactRead?>(null);

        Stream stream = File.OpenRead(stored.Path);
        return Task.FromResult<ArtifactRead?>(new ArtifactRead(stored.Artifact, stream));
    }

    private static async Task<(long Size, string Sha256)> WriteAndHashAsync(
        string path,
        Stream content,
        CancellationToken ct)
    {
        await using var file = File.Create(path);
        using var sha = SHA256.Create();

        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > MaxBytes)
                break;
            sha.TransformBlock(buffer, 0, read, null, 0);
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        sha.TransformFinalBlock([], 0, 0);
        return (total, Convert.ToHexStringLower(sha.Hash!));
    }

    private static bool ShaMatches(string declared, string actual) =>
        string.IsNullOrWhiteSpace(declared)
        || string.Equals(declared, actual, StringComparison.OrdinalIgnoreCase);

    private static string SafeName(string name)
    {
        var fileName = Path.GetFileName(name);
        return string.IsNullOrWhiteSpace(fileName) ? "artifact.bin" : fileName;
    }

    private sealed record StoredArtifact(Guid Owner, MissionArtifact Artifact, string Path);
}

public sealed class ArtifactTooLargeException(long maxBytes)
    : Exception($"Artifact exceeds the {maxBytes} byte upload limit.");
