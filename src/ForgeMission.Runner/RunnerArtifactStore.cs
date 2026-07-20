using System.Collections.Concurrent;
using System.Security.Cryptography;
using ForgeMission.Runner.Contracts;

namespace ForgeMission.Runner;

internal interface IRunnerArtifactStore
{
    Task<RunArtifact> SaveAsync(RunArtifactWriteRequest request, Stream content, CancellationToken ct);
    Task<RunnerArtifactRead?> OpenAsync(string artifactId, CancellationToken ct);
}

internal sealed record RunArtifactWriteRequest(
    string Name,
    string ContentType,
    string Sha256,
    string Role,
    long? DeclaredSize);

internal sealed record RunnerArtifactRead(RunArtifact Artifact, Stream Content) : IAsyncDisposable
{
    public async ValueTask DisposeAsync() => await Content.DisposeAsync();
}

internal sealed class RunnerArtifactStore(IConfiguration configuration) : IRunnerArtifactStore
{
    public const long MaxBytes = 100L * 1024L * 1024L;

    private readonly string _root = configuration["Artifacts:LocalRoot"]
        ?? Path.Combine(Path.GetTempPath(), "forge-runner-artifacts");

    private readonly ConcurrentDictionary<string, StoredArtifact> _artifacts = new();

    public async Task<RunArtifact> SaveAsync(
        RunArtifactWriteRequest request,
        Stream content,
        CancellationToken ct)
    {
        if (request.DeclaredSize is > MaxBytes)
            throw new ArtifactTooLargeException(MaxBytes);

        Directory.CreateDirectory(_root);

        var id = "run_art_" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(_root, id);
        var (size, sha256) = await WriteAndHashAsync(path, content, ct);
        if (size > MaxBytes)
        {
            File.Delete(path);
            throw new ArtifactTooLargeException(MaxBytes);
        }

        if (!string.IsNullOrWhiteSpace(request.Sha256)
            && !string.Equals(request.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new InvalidOperationException("Artifact SHA256 mismatch.");
        }

        var artifact = new RunArtifact(
            Id: id,
            Name: SafeName(request.Name),
            ContentType: string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType,
            Size: size,
            Sha256: sha256,
            Role: request.Role);

        _artifacts[id] = new StoredArtifact(artifact, path);
        return artifact;
    }

    public Task<RunnerArtifactRead?> OpenAsync(string artifactId, CancellationToken ct)
    {
        if (!_artifacts.TryGetValue(artifactId, out var stored) || !File.Exists(stored.Path))
            return Task.FromResult<RunnerArtifactRead?>(null);

        Stream stream = File.OpenRead(stored.Path);
        return Task.FromResult<RunnerArtifactRead?>(new RunnerArtifactRead(stored.Artifact, stream));
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

    private static string SafeName(string name)
    {
        var fileName = Path.GetFileName(name);
        return string.IsNullOrWhiteSpace(fileName) ? "artifact.bin" : fileName;
    }

    private sealed record StoredArtifact(RunArtifact Artifact, string Path);
}

internal sealed class ArtifactTooLargeException(long maxBytes)
    : Exception($"Artifact exceeds the {maxBytes} byte upload limit.");
