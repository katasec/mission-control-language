using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeMission.Rooms;
using Microsoft.Extensions.Logging;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Dev/local <see cref="IArtifactStore"/> — bytes on a local volume under <c>{root}/{roomId}/{id}</c>,
/// with a <c>.meta</c> sidecar holding the authoritative filename/content-type/size. In prod this is
/// swapped for an Azure Blob implementation behind the same seam (D2); nothing above the seam
/// changes. Membership is enforced through <see cref="IReadStore.IsMemberAsync"/>, the same check
/// messages use.
/// </summary>
public sealed class LocalVolumeArtifactStore(
    string root, IReadStore reads, ILogger<LocalVolumeArtifactStore> logger) : IArtifactStore
{
    public async Task<ArtifactRef> PutAsync(
        Guid roomId, string filename, string contentType, Stream content, CancellationToken ct = default)
    {
        var id  = Guid.NewGuid();
        var dir = Path.Combine(root, roomId.ToString());
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, id.ToString());
        long size;
        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, ct);
            size = file.Length;
        }

        var ct2  = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        var meta = new ArtifactMeta(filename, ct2, size);
        await File.WriteAllTextAsync(path + ".meta", JsonSerializer.Serialize(meta, ArtifactMetaContext.Default.ArtifactMeta), ct);

        logger.LogInformation("Stored artifact {Id} ({Bytes} bytes) for room {RoomId}", id, size, roomId);

        return new ArtifactRef
        {
            Id          = id,
            RoomId      = roomId,
            Filename    = filename,
            ContentType = ct2,
            Size        = size,
            // Storage key namespaced by room; membership is derivable from it and from RoomId.
            Key         = $"{roomId}/{id}",
        };
    }

    public async Task<ArtifactContent?> OpenAsync(
        Guid roomId, Guid artifactId, Guid requestingMemberId, CancellationToken ct = default)
    {
        // The one-line confidentiality gate (§4): the requester must be a member of the artifact's room.
        if (!await reads.IsMemberAsync(roomId, requestingMemberId, ct))
        {
            logger.LogWarning(
                "Denied artifact {Id}: member {MemberId} not in room {RoomId}",
                artifactId, requestingMemberId, roomId);
            return null;
        }

        // Path built from the typed Guids only (never a caller-supplied key) — Guids can't traverse.
        var path = Path.Combine(root, roomId.ToString(), artifactId.ToString());
        if (!File.Exists(path))
        {
            logger.LogWarning("Artifact {Id} missing on disk for room {RoomId}", artifactId, roomId);
            return null;
        }

        // Authoritative metadata comes from the sidecar, not the request.
        var meta = await ReadMetaAsync(path + ".meta", ct)
                   ?? new ArtifactMeta(artifactId.ToString(), "application/octet-stream", new FileInfo(path).Length);

        var stream = File.OpenRead(path);
        return new ArtifactContent(stream, meta.Filename, meta.ContentType, meta.Size);
    }

    private static async Task<ArtifactMeta?> ReadMetaAsync(string metaPath, CancellationToken ct)
    {
        if (!File.Exists(metaPath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(metaPath, ct);
            return JsonSerializer.Deserialize(json, ArtifactMetaContext.Default.ArtifactMeta);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Sidecar metadata persisted next to the bytes so a download names the file authoritatively.</summary>
internal sealed record ArtifactMeta(string Filename, string ContentType, long Size);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ArtifactMeta))]
internal partial class ArtifactMetaContext : JsonSerializerContext { }
